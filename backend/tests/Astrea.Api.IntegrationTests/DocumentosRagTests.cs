using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using Astrea.Application.Features.Knowledge;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Astrea.Api.IntegrationTests;

/// <summary>Ingesta documental (Slice 8): carga de PDF, indexación, versiones y errores.</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class DocumentosRagTests(AstreaApiFactory factory)
{
    private const string Base = "/api/v1/admin/documentos-rag";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    static DocumentosRagTests() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>PDF real de varias páginas. Lleva una marca única para que su hash no choque con el de otros tests.</summary>
    private static byte[] Pdf(params string[][] paginas)
    {
        var marca = Guid.NewGuid().ToString("N");
        return Document.Create(documento =>
        {
            foreach (var lineas in paginas)
            {
                documento.Page(pagina =>
                {
                    pagina.Margin(40);
                    pagina.Content().Column(columna =>
                    {
                        foreach (var linea in lineas)
                        {
                            columna.Item().Text(linea);
                        }
                    });
                    pagina.Footer().Text(marca).FontSize(6);
                });
            }
        }).GeneratePdf();
    }

    private static byte[] Reglamento() => Pdf(
        ["CAPÍTULO I", "DE LAS MESAS DE EXAMEN", "Artículo 1°: La inscripción a mesas cierra 48 horas antes de la fecha del examen."],
        ["CAPÍTULO II", "DE LA REGULARIDAD", "Artículo 2°: La regularidad se conserva durante dos años desde la cursada."]);

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> AdminAsync() => SesionAsync(AstreaApiFactory.AdministradorInicial);

    private static Task<HttpResponseMessage> CargarAsync(HttpClient cliente, byte[] archivo, string titulo, Guid? nuevaVersionDe = null, string nombre = "reglamento.pdf")
    {
        var formulario = new MultipartFormDataContent
        {
            { new StringContent(titulo), "titulo" },
            { new StringContent("Reglamento"), "tipo" },
            { new StringContent("Resolución 4043/09"), "fuente" },
            { new StringContent("2024-03-01"), "fechaDocumento" },
        };
        if (nuevaVersionDe is { } id)
        {
            formulario.Add(new StringContent(id.ToString()), "nuevaVersionDe");
        }

        var contenido = new ByteArrayContent(archivo);
        contenido.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(contenido, "archivo", nombre);
        return cliente.PostAsync(Base, formulario);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>Lo que hace el procesador en segundo plano (apagado en los tests para que sea determinista).</summary>
    private async Task ProcesarColaAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IngestaDocumentos>().ProcesarPendientesAsync(CancellationToken.None);
    }

    private static async Task<Documento> ObtenerAsync(HttpClient admin, Guid id) => (await LeerAsync<Detalle>(await admin.GetAsync($"{Base}/{id}"))).Documento;

    [Fact]
    public async Task La_secretaria_carga_un_pdf_y_queda_indexado_con_sus_fragmentos()
    {
        var admin = await AdminAsync();
        var archivo = Reglamento();

        var cargado = await LeerAsync<Documento>(await CargarAsync(admin, archivo, "Reglamento Académico Marco"));
        cargado.Estado.Should().Be("Pendiente");
        cargado.CargadoPor.Should().Be(AstreaApiFactory.AdministradorInicial);

        await ProcesarColaAsync();

        var indexado = await ObtenerAsync(admin, cargado.Id);
        indexado.Estado.Should().Be("Indexado");
        indexado.CantidadPaginas.Should().Be(2);
        indexado.CantidadFragmentos.Should().Be(2);

        var fragmentos = await LeerAsync<PaginaFragmentos>(await admin.GetAsync($"{Base}/{cargado.Id}/fragmentos"));
        fragmentos.Items[0].Should().BeEquivalentTo(new { NumeroFragmento = 1, Seccion = "CAPÍTULO I — DE LAS MESAS DE EXAMEN", PaginaDesde = 1 });
        fragmentos.Items[0].Contenido.Should().Contain("48 horas");
        fragmentos.Items[1].PaginaDesde.Should().Be(2);

        var descarga = await admin.GetAsync($"{Base}/{cargado.Id}/archivo");
        descarga.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await descarga.Content.ReadAsByteArrayAsync()).Should().Equal(archivo);
    }

    [Fact]
    public async Task Una_version_nueva_reemplaza_a_la_anterior_recien_cuando_se_indexa()
    {
        var admin = await AdminAsync();
        var titulo = $"Calendario {Guid.NewGuid():N}"[..20];
        var v1 = await LeerAsync<Documento>(await CargarAsync(admin, Reglamento(), titulo));
        await ProcesarColaAsync();

        var v2 = await LeerAsync<Documento>(await CargarAsync(admin, Reglamento(), titulo, nuevaVersionDe: v1.Id));
        v2.Version.Should().Be(2);
        (await ObtenerAsync(admin, v1.Id)).Estado.Should().Be("Indexado", "la anterior sigue vigente mientras la nueva está pendiente");

        await ProcesarColaAsync();

        var detalle = await LeerAsync<Detalle>(await admin.GetAsync($"{Base}/{v2.Id}"));
        detalle.Versiones.Select(version => (version.Version, version.Estado)).Should().Equal((2, "Indexado"), (1, "Reemplazado"));
        var listado = await LeerAsync<PaginaDocumentos>(await admin.GetAsync($"{Base}?texto={Uri.EscapeDataString(titulo)}"));
        listado.Items.Should().ContainSingle().Which.Id.Should().Be(v2.Id);
    }

    [Fact]
    public async Task Si_la_version_nueva_falla_queda_en_error_y_la_anterior_sigue_vigente()
    {
        var admin = await AdminAsync();
        var v1 = await LeerAsync<Documento>(await CargarAsync(admin, Reglamento(), "Régimen de correlatividades"));
        await ProcesarColaAsync();

        // Un PDF sin texto, como uno escaneado.
        var sinTexto = Document.Create(documento => documento.Page(pagina => pagina.Content().Height(10))).GeneratePdf();
        var v2 = await LeerAsync<Documento>(await CargarAsync(admin, sinTexto, "Régimen de correlatividades", nuevaVersionDe: v1.Id));
        await ProcesarColaAsync();

        var fallida = await ObtenerAsync(admin, v2.Id);
        fallida.Estado.Should().Be("Error");
        fallida.ErrorProcesamiento.Should().Contain("escaneado");
        (await ObtenerAsync(admin, v1.Id)).Estado.Should().Be("Indexado");

        var reindexar = await admin.PostAsync($"{Base}/{v1.Id}/reindexar", null);
        reindexar.StatusCode.Should().Be(HttpStatusCode.OK, "la versión nueva falló, así que la vigente se puede reindexar");
    }

    [Fact]
    public async Task Rechaza_archivos_que_no_son_pdf_y_archivos_repetidos()
    {
        var admin = await AdminAsync();
        var archivo = Reglamento();

        var html = await CargarAsync(admin, "<html>hola</html>"u8.ToArray(), "Falso", nombre: "falso.pdf");
        html.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await html.Content.ReadAsStringAsync()).Should().Contain("no es un PDF");

        (await CargarAsync(admin, archivo, "Original")).StatusCode.Should().Be(HttpStatusCode.Created);
        var repetido = await CargarAsync(admin, archivo, "Copia");
        repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repetido.Content.ReadAsStringAsync()).Should().Contain("Original");
    }

    [Fact]
    public async Task Retirar_lo_saca_de_las_consultas_y_no_se_puede_reindexar()
    {
        var admin = await AdminAsync();
        var documento = await LeerAsync<Documento>(await CargarAsync(admin, Reglamento(), "Comunicado derogado"));
        await ProcesarColaAsync();

        var retirado = await LeerAsync<Documento>(await admin.PostAsync($"{Base}/{documento.Id}/retirar", null));

        retirado.Estado.Should().Be("Retirado");
        (await admin.PostAsync($"{Base}/{documento.Id}/reindexar", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_estudiante_no_accede_a_los_documentos_del_asistente()
    {
        var estudiante = await SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

        (await estudiante.GetAsync(Base)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CargarAsync(estudiante, Reglamento(), "Intento")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync(Base)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record Documento(Guid Id, int Version, string Titulo, string Estado, string? ErrorProcesamiento, int? CantidadPaginas, int? CantidadFragmentos, string CargadoPor);

    private sealed record Detalle(Documento Documento, List<Documento> Versiones);

    private sealed record PaginaDocumentos(List<Documento> Items, int TotalItems);

    private sealed record Fragmento(int NumeroFragmento, string Contenido, string? Seccion, int PaginaDesde, int PaginaHasta);

    private sealed record PaginaFragmentos(List<Fragmento> Items, int TotalItems);
}
