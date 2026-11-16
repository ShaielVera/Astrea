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

/// <summary>
/// Asistente RAG (Slice 9) de punta a punta con un corpus controlado y la IA de prueba (sin red):
/// responde con fuentes, se abstiene sin evidencia, mantiene el historial y cada usuario ve solo lo suyo.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class AsistenteTests(AstreaApiFactory factory)
{
    private const string Base = "/api/v1/asistente";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    static AsistenteTests() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>Palabra inventada y única por test: la búsqueda encuentra este documento y no los de otros tests.</summary>
    private readonly string _marca = $"zq{Guid.NewGuid():N}"[..10];

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    /// <summary>La Secretaría carga un reglamento con la marca del test y se indexa.</summary>
    private async Task<Guid> CargarReglamentoAsync()
    {
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var pdf = Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Margin(40);
            pagina.Content().Column(columna =>
            {
                columna.Item().Text("CAPÍTULO I");
                columna.Item().Text("DE LAS MESAS DE EXAMEN");
                columna.Item().Text($"Artículo 1°: La inscripción a mesas {_marca} cierra 48 horas antes de la fecha del examen.");
            });
        })).GeneratePdf();

        var formulario = new MultipartFormDataContent
        {
            { new StringContent($"Reglamento {_marca}"), "titulo" },
            { new StringContent("Reglamento"), "tipo" },
            { new StringContent("Resolución 4043/09"), "fuente" },
        };
        var archivo = new ByteArrayContent(pdf);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(archivo, "archivo", "reglamento.pdf");
        var cargado = await LeerAsync<Documento>(await admin.PostAsync("/api/v1/admin/documentos-rag", formulario));

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IngestaDocumentos>().ProcesarPendientesAsync(CancellationToken.None);
        return cargado.Id;
    }

    private static Task<HttpResponseMessage> PreguntarAsync(HttpClient cliente, string mensaje, Guid? conversacionId = null) =>
        cliente.PostAsJsonAsync($"{Base}/preguntas", new { conversacionId, mensaje });

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    [Fact]
    public async Task Responde_una_pregunta_sobre_un_reglamento_cargado_citando_la_fuente()
    {
        var documentoId = await CargarReglamentoAsync();
        var estudiante = await EstudianteAsync();

        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, $"¿Cuándo cierra la inscripción a mesas {_marca}?"));

        intercambio.Respuesta.SinEvidencia.Should().BeFalse();
        intercambio.Respuesta.Contenido.Should().Contain("cierra 48 horas antes").And.EndWith("[1]");
        var fuente = intercambio.Respuesta.Fuentes.Should().ContainSingle().Subject;
        fuente.Should().BeEquivalentTo(new { DocumentoId = documentoId, Titulo = $"Reglamento {_marca}", Version = 1, Seccion = "CAPÍTULO I — DE LAS MESAS DE EXAMEN", Fuente = "Resolución 4043/09", PaginaDesde = 1 });

        // El estudiante puede abrir el PDF citado.
        var pdf = await estudiante.GetAsync($"{Base}/fuentes/{documentoId}");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Sin_evidencia_reconoce_que_no_sabe_y_deriva_a_secretaria()
    {
        var estudiante = await EstudianteAsync();

        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, $"¿Quién ganó el campeonato {_marca} de ajedrez?"));

        intercambio.Respuesta.SinEvidencia.Should().BeTrue();
        intercambio.Respuesta.Fuentes.Should().BeEmpty();
        intercambio.Respuesta.Contenido.Should().Contain("No encontré información confirmada").And.Contain("Secretaría");
    }

    [Fact]
    public async Task Mantiene_el_historial_y_cada_usuario_ve_solo_sus_conversaciones()
    {
        await CargarReglamentoAsync();
        var estudiante = await EstudianteAsync();
        var otro = await EstudianteAsync();

        var primera = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, $"¿Cuándo cierra la inscripción a mesas {_marca}? Mi DNI es 35123456"));
        primera.Pregunta.Contenido.Should().EndWith("Mi DNI es [DNI]", "el DNI no se guarda ni se envía a la IA");
        await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, "¿Y si no llego a anotarme?", primera.ConversacionId));

        var conversacion = await LeerAsync<Conversacion>(await estudiante.GetAsync($"{Base}/conversaciones/{primera.ConversacionId}"));
        conversacion.Mensajes.Select(mensaje => mensaje.Rol).Should().Equal("Usuario", "Asistente", "Usuario", "Asistente");
        conversacion.Titulo.Should().NotContain("35123456");

        var mias = await LeerAsync<PaginaConversaciones>(await estudiante.GetAsync($"{Base}/conversaciones"));
        mias.Items.Should().ContainSingle().Which.Id.Should().Be(primera.ConversacionId);
        (await LeerAsync<PaginaConversaciones>(await otro.GetAsync($"{Base}/conversaciones"))).Items.Should().BeEmpty();

        // Otra persona no puede verla, seguirla ni borrarla (y no se entera de que existe).
        (await otro.GetAsync($"{Base}/conversaciones/{primera.ConversacionId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PreguntarAsync(otro, "Hola", primera.ConversacionId)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otro.DeleteAsync($"{Base}/conversaciones/{primera.ConversacionId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await estudiante.DeleteAsync($"{Base}/conversaciones/{primera.ConversacionId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await estudiante.GetAsync($"{Base}/conversaciones/{primera.ConversacionId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Valida_la_pregunta_y_exige_sesion()
    {
        var estudiante = await EstudianteAsync();

        (await PreguntarAsync(estudiante, "   ")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PreguntarAsync(estudiante, new string('a', 1001))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PreguntarAsync(factory.CreateClient(), "Hola")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_documento_retirado_ya_no_responde_ni_se_puede_descargar()
    {
        var documentoId = await CargarReglamentoAsync();
        var admin = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        (await admin.PostAsync($"/api/v1/admin/documentos-rag/{documentoId}/retirar", null)).EnsureSuccessStatusCode();
        var estudiante = await EstudianteAsync();

        var intercambio = await LeerAsync<Intercambio>(await PreguntarAsync(estudiante, $"¿Cuándo cierra la inscripción a mesas {_marca}?"));

        intercambio.Respuesta.Fuentes.Should().NotContain(fuente => fuente.DocumentoId == documentoId);
        (await estudiante.GetAsync($"{Base}/fuentes/{documentoId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record Documento(Guid Id);

    private sealed record FuenteCitada(Guid DocumentoId, string Titulo, int Version, string? Seccion, string? Fuente, int PaginaDesde, int PaginaHasta, string Extracto);

    private sealed record Mensaje(Guid Id, string Rol, string Contenido, List<FuenteCitada> Fuentes, bool SinEvidencia);

    private sealed record Intercambio(Guid ConversacionId, string Titulo, Mensaje Pregunta, Mensaje Respuesta);

    private sealed record Conversacion(Guid Id, string Titulo, List<Mensaje> Mensajes);

    private sealed record ConversacionResumen(Guid Id, string Titulo);

    private sealed record PaginaConversaciones(List<ConversacionResumen> Items, int TotalItems);
}
