using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Features.Knowledge;
using Astrea.Domain.Knowledge;
using Astrea.Domain.Users;
using Astrea.Infrastructure.Ai;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

/// <summary>Fragmentos con embeddings reales en pgvector: guardado transaccional, versiones y búsqueda por similitud.</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class EfDocumentoRagRepositoryTests(BaseDeDatosFixture baseDeDatos)
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Palabra única por test: los documentos de otros tests no interfieren en la búsqueda.</summary>
    private readonly string _marca = $"zq{Guid.NewGuid():N}"[..12];

    private static string HashAleatorio() => Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()) + Convert.ToHexStringLower(Guid.NewGuid().ToByteArray());

    private async Task<Usuario> CrearSecretariaAsync()
    {
        await using var db = baseDeDatos.CrearContexto();
        var secretaria = Usuario.Registrar(Guid.NewGuid(), $"uid-{Guid.NewGuid():N}", $"secretaria-{Guid.NewGuid():N}@astrea.test", Ahora);
        db.Add(secretaria);
        await db.SaveChangesAsync();
        return secretaria;
    }

    /// <summary>Carga, procesa e indexa un documento con un fragmento por texto (embeddings de prueba).</summary>
    private async Task<DocumentoRag> IndexarAsync(Usuario secretaria, string titulo, DocumentoRag? anterior, params string[] textos)
    {
        await using var db = baseDeDatos.CrearContexto();
        var repositorio = new EfDocumentoRagRepository(db);
        var datos = new DatosDocumentoRag(titulo, TipoDocumentoRag.Reglamento, "Secretaría", null);
        var documento = anterior is null
            ? DocumentoRag.Cargar(Guid.NewGuid(), datos, $"documentos-rag/{Guid.NewGuid()}.pdf", HashAleatorio(), secretaria.Id, Ahora)
            : anterior.NuevaVersion(Guid.NewGuid(), anterior.Version, datos, $"documentos-rag/{Guid.NewGuid()}.pdf", HashAleatorio(), secretaria.Id, Ahora);
        repositorio.Agregar(documento);
        await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);

        documento.IniciarProcesamiento();
        documento.MarcarIndexado(1, textos.Length, Ahora);
        var fragmentos = await FragmentosAsync(documento, textos);
        await repositorio.GuardarIndexacionAsync(documento.Id, fragmentos, CancellationToken.None);
        return documento;
    }

    private static async Task<List<FragmentoRag>> FragmentosAsync(DocumentoRag documento, string[] textos)
    {
        var vectores = await new EmbeddingsDePrueba().GenerarAsync(textos, PropositoEmbedding.Documento, CancellationToken.None);
        return textos.Select((texto, i) => new FragmentoRag(
                Guid.NewGuid(),
                documento.Id,
                i + 1,
                texto,
                vectores[i],
                new MetadataFragmento(documento.Titulo, documento.Tipo, documento.Version, $"Capítulo {i + 1}", documento.Fuente, null),
                i + 1,
                i + 1))
            .ToList();
    }

    private async Task<IReadOnlyList<FragmentoEncontrado>> BuscarAsync(string pregunta)
    {
        await using var db = baseDeDatos.CrearContexto();
        var vector = (await new EmbeddingsDePrueba().GenerarAsync([pregunta], PropositoEmbedding.Consulta, CancellationToken.None))[0];
        return await new EfDocumentoRagRepository(db).BuscarSimilaresAsync(vector, 5, CancellationToken.None);
    }

    [Fact]
    public async Task Busca_por_similitud_coseno_con_pgvector_y_devuelve_los_metadatos()
    {
        var secretaria = await CrearSecretariaAsync();
        var documento = await IndexarAsync(
            secretaria,
            "Reglamento de mesas",
            null,
            $"{_marca} inscripción mesas examen cierra cuarenta ocho horas antes",
            $"{_marca} regularidad asistencia ochenta por ciento clases");

        var encontrados = await BuscarAsync($"{_marca} cuándo cierra la inscripción a mesas de examen");

        var primero = encontrados[0];
        primero.DocumentoId.Should().Be(documento.Id);
        primero.Contenido.Should().Contain("inscripción mesas");
        primero.Metadata.Should().Be(new MetadataFragmento("Reglamento de mesas", TipoDocumentoRag.Reglamento, 1, "Capítulo 1", "Secretaría", null));
        primero.Similitud.Should().BeGreaterThan(encontrados.Single(f => f.DocumentoId == documento.Id && f.NumeroFragmento == 2).Similitud);
    }

    [Fact]
    public async Task Solo_busca_en_la_version_vigente_y_no_en_las_reemplazadas_ni_retiradas()
    {
        var secretaria = await CrearSecretariaAsync();
        var v1 = await IndexarAsync(secretaria, "Calendario", null, $"{_marca} turno febrero versión vieja");
        var v2 = await IndexarAsync(secretaria, "Calendario", v1, $"{_marca} turno febrero versión nueva");
        await using (var db = baseDeDatos.CrearContexto())
        {
            var repositorio = new EfDocumentoRagRepository(db);
            (await repositorio.ObtenerAsync(v1.Id, CancellationToken.None))!.MarcarReemplazado();
            await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
        }

        var encontrados = await BuscarAsync($"{_marca} turno febrero");

        encontrados.Should().Contain(f => f.DocumentoId == v2.Id).And.NotContain(f => f.DocumentoId == v1.Id);

        await using (var db = baseDeDatos.CrearContexto())
        {
            (await new EfDocumentoRagRepository(db).ObtenerAsync(v2.Id, CancellationToken.None))!.Retirar(secretaria.Id);
            await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
        }

        (await BuscarAsync($"{_marca} turno febrero")).Should().NotContain(f => f.DocumentoId == v2.Id);
    }

    [Fact]
    public async Task Reindexar_reemplaza_los_fragmentos_anteriores()
    {
        var secretaria = await CrearSecretariaAsync();
        var documento = await IndexarAsync(secretaria, "Plan", null, $"{_marca} uno", $"{_marca} dos", $"{_marca} tres");

        await using (var db = baseDeDatos.CrearContexto())
        {
            var repositorio = new EfDocumentoRagRepository(db);
            var guardado = (await repositorio.ObtenerAsync(documento.Id, CancellationToken.None))!;
            guardado.Reindexar();
            guardado.IniciarProcesamiento();
            guardado.MarcarIndexado(1, 1, Ahora);
            await repositorio.GuardarIndexacionAsync(guardado.Id, await FragmentosAsync(guardado, [$"{_marca} único"]), CancellationToken.None);
        }

        await using var lectura = baseDeDatos.CrearContexto();
        var fragmentos = await new EfDocumentoRagRepository(lectura).ListarFragmentosAsync(documento.Id, PageRequest.Create(1, 10), CancellationToken.None);
        fragmentos.Items.Should().ContainSingle().Which.Contenido.Should().Be($"{_marca} único");
        (await new EfDocumentoRagRepository(lectura).ObtenerAsync(documento.Id, CancellationToken.None))!.Intentos.Should().Be(2);
    }

    [Fact]
    public async Task El_listado_muestra_solo_la_version_mas_nueva_de_cada_documento()
    {
        var secretaria = await CrearSecretariaAsync();
        var v1 = await IndexarAsync(secretaria, $"Régimen {_marca}", null, "texto");
        var v2 = await IndexarAsync(secretaria, $"Régimen {_marca}", v1, "texto nuevo");

        await using var db = baseDeDatos.CrearContexto();
        var listado = await new EfDocumentoRagRepository(db).ListarAsync(new FiltroDocumentosRag(Texto: $"regimen {_marca}"), PageRequest.Create(1, 10), CancellationToken.None);

        listado.Items.Should().ContainSingle().Which.Id.Should().Be(v2.Id);
    }
}
