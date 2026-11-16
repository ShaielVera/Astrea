using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Abstractions.Clock;
using Astrea.Application.Abstractions.Documents;
using Astrea.Application.Abstractions.Identity;
using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Auth;
using Astrea.Application.Features.Knowledge;
using Astrea.Application.Features.Procedures;
using Astrea.Domain.Common;
using Astrea.Domain.Knowledge;
using Astrea.Domain.Users;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Astrea.Application.UnitTests.Features.Knowledge;

public class DocumentosRagTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Pdf = "%PDF-1.7 contenido"u8.ToArray();

    private readonly IDocumentoRagRepository _documentos = Substitute.For<IDocumentoRagRepository>();
    private readonly IFileStorage _archivos = Substitute.For<IFileStorage>();
    private readonly IExtractorTextoPdf _extractor = Substitute.For<IExtractorTextoPdf>();
    private readonly IEmbeddingService _embeddings = Substitute.For<IEmbeddingService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAvisoIngesta _aviso = Substitute.For<IAvisoIngesta>();
    private readonly ICurrentUser _sesion = Substitute.For<ICurrentUser>();
    private readonly IUserRepository _usuarios = Substitute.For<IUserRepository>();
    private readonly Usuario _secretaria = Usuario.Registrar(Guid.NewGuid(), "uid-secretaria", "secretaria@astrea.test", Ahora);

    public DocumentosRagTests()
    {
        _clock.UtcNow.Returns(Ahora);
        _sesion.FirebaseUid.Returns("uid-secretaria");
        _usuarios.ObtenerPorFirebaseUidAsync("uid-secretaria", Arg.Any<CancellationToken>()).Returns(_secretaria);
        _documentos.ObtenerPersonasAsync(default!, default).ReturnsForAnyArgs(new Dictionary<Guid, PersonaDto>());
        _embeddings.GenerarAsync(default!, default, default).ReturnsForAnyArgs(llamada =>
            Task.FromResult<IReadOnlyList<float[]>>((llamada.Arg<IReadOnlyList<string>>() ?? []).Select(_ => Vector()).ToList()));
    }

    private static float[] Vector()
    {
        var vector = new float[FragmentoRag.DimensionesEmbedding];
        vector[0] = 1;
        return vector;
    }

    private static DocumentoRag Pendiente(string hash = "a") =>
        DocumentoRag.Cargar(Guid.NewGuid(), new DatosDocumentoRag("Reglamento Académico", TipoDocumentoRag.Reglamento, null, null), "documentos-rag/2026/x.pdf", new string(hash[0], 64), Guid.NewGuid(), Ahora);

    private AdministracionDocumentosRag Administracion() =>
        new(new UsuarioActual(_sesion, _usuarios), _documentos, _archivos, _unitOfWork, _clock, _aviso);

    private IngestaDocumentos Ingesta() => new(_documentos, _archivos, _extractor, _embeddings, _unitOfWork, _clock);

    /// <summary>Deja <paramref name="documento"/> como el próximo de la cola, con un PDF de <paramref name="paginas"/> páginas.</summary>
    private void EnCola(DocumentoRag documento, params string[] paginas)
    {
        _documentos.ObtenerSiguientePendienteAsync(Arg.Any<CancellationToken>()).Returns(documento, (DocumentoRag?)null);
        _documentos.ObtenerAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        _documentos.ObtenerSerieAsync(documento.SerieId, Arg.Any<CancellationToken>()).Returns([documento]);
        _archivos.LeerAsync(documento.StoragePath, Arg.Any<CancellationToken>()).Returns(Pdf);
        _extractor.Extraer(Pdf).Returns(paginas.Select((texto, i) => new PaginaTexto(i + 1, texto)).ToList());
    }

    [Fact]
    public async Task Cargar_guarda_el_archivo_lo_deja_pendiente_y_avisa_al_procesador()
    {
        var resultado = await Administracion().CargarAsync(new CargarDocumentoRagCommand("Reglamento", TipoDocumentoRag.Reglamento, null, null, null), Pdf, CancellationToken.None);

        resultado.Valor.Estado.Should().Be(EstadoIndexacion.Pendiente);
        resultado.Valor.Version.Should().Be(1);
        await _archivos.Received(1).GuardarAsync(Arg.Is<string>(ruta => ruta.StartsWith("documentos-rag/2026/") && ruta.EndsWith(".pdf")), Pdf, Arg.Any<CancellationToken>());
        _aviso.Received(1).Avisar();
    }

    [Fact]
    public async Task Un_archivo_ya_cargado_no_se_vuelve_a_cargar()
    {
        _documentos.BuscarPorHashAsync(default!, default).ReturnsForAnyArgs(Pendiente());

        var resultado = await Administracion().CargarAsync(new CargarDocumentoRagCommand("Otro título", TipoDocumentoRag.Otro, null, null, null), Pdf, CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.Conflicto);
        resultado.Error.Mensaje.Should().Contain("Reglamento Académico");
        await _archivos.DidNotReceiveWithAnyArgs().GuardarAsync(default!, default!, default);
    }

    [Fact]
    public async Task Un_archivo_que_no_es_pdf_se_rechaza_antes_de_guardar()
    {
        var cargar = () => Administracion().CargarAsync(new CargarDocumentoRagCommand("X", TipoDocumentoRag.Otro, null, null, null), "<html>"u8.ToArray(), CancellationToken.None);

        await cargar.Should().ThrowAsync<DomainException>().WithMessage("*no es un PDF*");
        await _archivos.DidNotReceiveWithAnyArgs().GuardarAsync(default!, default!, default);
    }

    [Fact]
    public async Task Indexa_un_documento_con_sus_fragmentos_y_metadatos()
    {
        var documento = Pendiente();
        EnCola(documento, "CAPÍTULO I\nLas mesas se constituyen en tres turnos.", "La inscripción cierra 48 horas antes.");
        IReadOnlyList<FragmentoRag>? guardados = null;
        await _documentos.GuardarIndexacionAsync(documento.Id, Arg.Do<IReadOnlyList<FragmentoRag>>(lista => guardados = lista), Arg.Any<CancellationToken>());

        var resultados = await Ingesta().ProcesarPendientesAsync(CancellationToken.None);

        resultados.Should().ContainSingle().Which.Indexado.Should().BeTrue();
        documento.Estado.Should().Be(EstadoIndexacion.Indexado);
        documento.CantidadPaginas.Should().Be(2);
        guardados.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PaginaDesde = 1,
            PaginaHasta = 2,
            Metadata = new MetadataFragmento("Reglamento Académico", TipoDocumentoRag.Reglamento, 1, "CAPÍTULO I", null, null),
        });
        await _embeddings.Received(1).GenerarAsync(
            Arg.Is<IReadOnlyList<string>>(textos => textos[0].StartsWith("Reglamento Académico — CAPÍTULO I")),
            PropositoEmbedding.Documento,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_pdf_sin_texto_queda_en_error_con_un_mensaje_claro()
    {
        var documento = Pendiente();
        EnCola(documento, "  ", "");

        var resultado = (await Ingesta().ProcesarPendientesAsync(CancellationToken.None)).Single();

        resultado.Indexado.Should().BeFalse();
        documento.Estado.Should().Be(EstadoIndexacion.Error);
        documento.ErrorProcesamiento.Should().Contain("escaneado");
        await _documentos.DidNotReceiveWithAnyArgs().GuardarIndexacionAsync(default, default!, default);
    }

    [Fact]
    public async Task Si_falla_una_tanda_de_embeddings_no_se_guarda_nada_y_la_version_anterior_sigue_vigente()
    {
        var anterior = Pendiente("a");
        anterior.IniciarProcesamiento();
        anterior.MarcarIndexado(1, 3, Ahora);
        var nueva = anterior.NuevaVersion(Guid.NewGuid(), 1, new DatosDocumentoRag("Reglamento Académico", TipoDocumentoRag.Reglamento, null, null), "documentos-rag/2026/y.pdf", new string('b', 64), Guid.NewGuid(), Ahora);
        var paginas = Enumerable.Range(1, 80).Select(i => $"Artículo {i}°: " + string.Concat(Enumerable.Repeat($"El estudiante cumple la condición {i}. ", 30))).ToArray();
        EnCola(nueva, paginas);
        _documentos.ObtenerSerieAsync(nueva.SerieId, Arg.Any<CancellationToken>()).Returns([nueva, anterior]);
        var llamadas = 0;
        _embeddings.GenerarAsync(default!, default, default).ReturnsForAnyArgs(llamada => ++llamadas == 2
            ? throw new EmbeddingNoDisponibleException("Se alcanzó el límite de uso de Gemini.")
            : Task.FromResult<IReadOnlyList<float[]>>(llamada.Arg<IReadOnlyList<string>>().Select(_ => Vector()).ToList()));

        var resultado = (await Ingesta().ProcesarPendientesAsync(CancellationToken.None)).Single();

        llamadas.Should().Be(2, "el documento necesita varias tandas y falla la segunda");
        resultado.Error.Should().Be("Se alcanzó el límite de uso de Gemini.");
        nueva.Estado.Should().Be(EstadoIndexacion.Error);
        anterior.Estado.Should().Be(EstadoIndexacion.Indexado);
        _unitOfWork.Received(1).DescartarCambios();
        await _documentos.DidNotReceiveWithAnyArgs().GuardarIndexacionAsync(default, default!, default);
    }

    [Fact]
    public async Task Al_indexar_una_version_nueva_la_anterior_queda_reemplazada()
    {
        var anterior = Pendiente("a");
        anterior.IniciarProcesamiento();
        anterior.MarcarIndexado(1, 3, Ahora);
        var nueva = anterior.NuevaVersion(Guid.NewGuid(), 1, new DatosDocumentoRag("Reglamento Académico", TipoDocumentoRag.Reglamento, null, null), "documentos-rag/2026/y.pdf", new string('b', 64), Guid.NewGuid(), Ahora);
        EnCola(nueva, "Texto de la versión 2.");
        _documentos.ObtenerSerieAsync(nueva.SerieId, Arg.Any<CancellationToken>()).Returns([nueva, anterior]);

        await Ingesta().ProcesarPendientesAsync(CancellationToken.None);

        nueva.Estado.Should().Be(EstadoIndexacion.Indexado);
        anterior.Estado.Should().Be(EstadoIndexacion.Reemplazado);
    }

    [Fact]
    public async Task Un_error_inesperado_no_muestra_detalles_internos()
    {
        var documento = Pendiente();
        EnCola(documento, "Texto.");
        _extractor.Extraer(Pdf).Throws(new InvalidOperationException("detalle interno"));

        var resultado = (await Ingesta().ProcesarPendientesAsync(CancellationToken.None)).Single();

        documento.ErrorProcesamiento.Should().Be(IngestaDocumentos.ErrorInesperado);
        resultado.Excepcion.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task No_se_reindexa_una_version_vieja_si_hay_una_mas_nueva_vigente()
    {
        var anterior = Pendiente("a");
        anterior.IniciarProcesamiento();
        anterior.MarcarError("x");
        var nueva = anterior.NuevaVersion(Guid.NewGuid(), 1, new DatosDocumentoRag("Reglamento", TipoDocumentoRag.Reglamento, null, null), "documentos-rag/2026/y.pdf", new string('b', 64), Guid.NewGuid(), Ahora);
        _documentos.ObtenerAsync(anterior.Id, Arg.Any<CancellationToken>()).Returns(anterior);
        _documentos.ObtenerSerieAsync(anterior.SerieId, Arg.Any<CancellationToken>()).Returns([nueva, anterior]);

        var resultado = await Administracion().ReindexarAsync(anterior.Id, CancellationToken.None);

        resultado.Error.Should().Be(ErroresDocumentosRag.HayVersionMasNueva);
        anterior.Estado.Should().Be(EstadoIndexacion.Error);
    }

    [Fact]
    public async Task Al_arrancar_lo_que_quedo_procesando_vuelve_a_la_cola()
    {
        var documento = Pendiente();
        documento.IniciarProcesamiento();
        _documentos.ListarProcesandoAsync(Arg.Any<CancellationToken>()).Returns([documento]);

        (await Ingesta().RecuperarInterrumpidosAsync(CancellationToken.None)).Should().Be(1);

        documento.Estado.Should().Be(EstadoIndexacion.Pendiente);
    }
}
