using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astrea.Api.IntegrationTests.Infrastructure;
using Astrea.Domain.Procedures;
using AwesomeAssertions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;

namespace Astrea.Api.IntegrationTests;

/// <summary>Emisión, descarga y validación del PDF oficial (Slice 6).</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class DocumentosTests(AstreaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    private Task<HttpClient> AdminAsync() => SesionAsync(AstreaApiFactory.AdministradorInicial);

    private Task<HttpClient> EstudianteAsync() => SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>Una carrera, un estudiante con su pedido de constancia de alumno regular y la bandeja lista para aprobar.</summary>
    private async Task<(HttpClient Admin, HttpClient Estudiante, Detalle Tramite)> PedirConstanciaAsync(Guid? tipoId = null, Dictionary<string, string?>? datosExtra = null)
    {
        var admin = await AdminAsync();
        var codigo = $"D{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var carrera = await LeerAsync<IdDto>(await admin.PostAsJsonAsync("/api/v1/admin/carreras", new { codigo, nombre = $"Profesorado de Prueba {codigo}", duracionAnios = 4 }));
        var estudiante = await EstudianteAsync();

        var datos = datosExtra ?? new Dictionary<string, string?> { ["presentarAnte"] = "ANSES" };
        datos["carrera"] = carrera.Id.ToString();
        var tramite = await LeerAsync<Detalle>(await estudiante.PostAsJsonAsync("/api/v1/tramites", new { tipoId = tipoId ?? CatalogoTramites.AlumnoRegularId, datos }));
        return (admin, estudiante, tramite);
    }

    private static async Task CargarDatosOficialesAsync(HttpClient admin, Detalle tramite) =>
        await LeerAsync<Persona>(await admin.PutAsJsonAsync(
            $"/api/v1/admin/usuarios/{tramite.Solicitante.Id}/datos-oficiales",
            new { nombre = "Ana", apellido = "Pérez", dni = "30.123.456", legajo = "L-2026-001" }));

    private static Task<HttpResponseMessage> AprobarAsync(HttpClient admin, Guid tramiteId) =>
        admin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramiteId}/aprobar", new { comentario = "Ya podés descargarla" });

    [Fact]
    public async Task Sin_datos_oficiales_no_se_aprueba_y_con_ellos_se_emite_el_pdf()
    {
        var (admin, _, tramite) = await PedirConstanciaAsync();

        var sinDatos = await AprobarAsync(admin, tramite.Id);
        sinDatos.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await sinDatos.Content.ReadAsStringAsync()).Should().Contain("datos oficiales");

        await CargarDatosOficialesAsync(admin, tramite);
        var aprobado = await LeerAsync<Detalle>(await AprobarAsync(admin, tramite.Id));

        aprobado.Estado.Should().Be("Listo");
        aprobado.EmiteDocumento.Should().BeTrue();
        aprobado.Documento!.Estado.Should().Be("Vigente");
        aprobado.Documento.VersionPlantilla.Should().Be(1);
        aprobado.Documento.CodigoValidacion.Should().MatchRegex("^[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$");
        aprobado.Documento.UrlValidacion.Should().Be($"https://astrea.test/validar/{aprobado.Documento.CodigoValidacion}");
    }

    [Fact]
    public async Task El_titular_descarga_un_pdf_privado_con_sus_datos_el_codigo_y_el_hash_registrado()
    {
        var (admin, estudiante, tramite) = await PedirConstanciaAsync();
        await CargarDatosOficialesAsync(admin, tramite);
        var documento = (await LeerAsync<Detalle>(await AprobarAsync(admin, tramite.Id))).Documento!;

        var descarga = await estudiante.GetAsync($"/api/v1/tramites/{tramite.Id}/documento");

        descarga.StatusCode.Should().Be(HttpStatusCode.OK);
        descarga.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        descarga.Content.Headers.ContentDisposition!.FileName.Should().Contain(tramite.Numero);
        descarga.Headers.CacheControl!.Private.Should().BeTrue();
        descarga.Headers.CacheControl.NoStore.Should().BeTrue();

        var pdf = await descarga.Content.ReadAsByteArrayAsync();
        Convert.ToHexStringLower(SHA256.HashData(pdf)).Should().Be(documento.HashSha256);

        using var leido = PdfDocument.Open(pdf);
        var texto = string.Join(" ", leido.GetPages().Select(pagina => pagina.Text));
        texto.Should().Contain("CONSTANCIA DE ALUMNO REGULAR")
            .And.Contain("Ana Pérez")
            .And.Contain("30.123.456")
            .And.Contain("Profesorado de Prueba")
            .And.Contain("ANSES")
            .And.Contain(documento.CodigoValidacion)
            .And.Contain(tramite.Numero);
    }

    [Fact]
    public async Task Nadie_mas_que_el_titular_descarga_el_documento()
    {
        var (admin, _, tramite) = await PedirConstanciaAsync();
        await CargarDatosOficialesAsync(admin, tramite);
        (await AprobarAsync(admin, tramite.Id)).EnsureSuccessStatusCode();

        var otroEstudiante = await (await EstudianteAsync()).GetAsync($"/api/v1/tramites/{tramite.Id}/documento");
        var sinSesion = await factory.CreateClient().GetAsync($"/api/v1/tramites/{tramite.Id}/documento");
        var administracion = await admin.GetAsync($"/api/v1/admin/tramites/{tramite.Id}/documento");

        otroEstudiante.StatusCode.Should().Be(HttpStatusCode.NotFound);
        sinSesion.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        administracion.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task La_validacion_publica_no_pide_sesion_y_muestra_solo_datos_minimos()
    {
        var (admin, _, tramite) = await PedirConstanciaAsync();
        await CargarDatosOficialesAsync(admin, tramite);
        var documento = (await LeerAsync<Detalle>(await AprobarAsync(admin, tramite.Id))).Documento!;
        var publico = factory.CreateClient();

        var respuesta = await publico.GetAsync($"/api/v1/documentos/validar/{documento.CodigoValidacion.ToLowerInvariant()}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await respuesta.Content.ReadAsStringAsync();
        var validacion = JsonSerializer.Deserialize<Validacion>(json, Json)!;
        validacion.TipoDocumento.Should().Be("Constancia de alumno regular");
        validacion.Estado.Should().Be("Vigente");
        validacion.DniParcial.Should().Be("**.***.456");
        validacion.NumeroTramite.Should().Be(tramite.Numero);
        validacion.HashSha256.Should().Be(documento.HashSha256);
        json.Should().NotContain("Pérez").And.NotContain("30123456").And.NotContain("30.123.456").And.NotContain("@astrea.test");

        (await publico.GetAsync("/api/v1/documentos/validar/ZZZZ-ZZZZ-ZZZZ")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Un_documento_anulado_no_se_descarga_y_la_validacion_lo_muestra_anulado()
    {
        var (admin, estudiante, tramite) = await PedirConstanciaAsync();
        await CargarDatosOficialesAsync(admin, tramite);
        var documento = (await LeerAsync<Detalle>(await AprobarAsync(admin, tramite.Id))).Documento!;

        var anulado = await LeerAsync<Detalle>(await admin.PostAsJsonAsync($"/api/v1/admin/tramites/{tramite.Id}/documento/anulacion", new { motivo = "Se emitió con la carrera equivocada" }));

        anulado.Documento!.Estado.Should().Be("Anulado");
        (await estudiante.GetAsync($"/api/v1/tramites/{tramite.Id}/documento")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var validacion = await LeerAsync<Validacion>(await factory.CreateClient().GetAsync($"/api/v1/documentos/validar/{documento.CodigoValidacion}"));
        validacion.Estado.Should().Be("Anulado");
    }

    [Fact]
    public async Task Aprobar_dos_veces_emite_un_solo_documento()
    {
        var (admin, _, tramite) = await PedirConstanciaAsync();
        await CargarDatosOficialesAsync(admin, tramite);

        var primera = await LeerAsync<Detalle>(await AprobarAsync(admin, tramite.Id));
        var segunda = await LeerAsync<Detalle>(await AprobarAsync(admin, tramite.Id));

        segunda.Documento!.CodigoValidacion.Should().Be(primera.Documento!.CodigoValidacion);
        await factory.EjecutarEnBaseAsync(async db =>
            (await db.DocumentosEmitidos.CountAsync(documento => documento.TramiteId == tramite.Id)).Should().Be(1));
        Directory.GetFiles(factory.CarpetaArchivos, $"{primera.Documento.Id}.pdf", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Fact]
    public async Task Una_version_nueva_de_la_plantilla_se_usa_en_las_proximas_emisiones()
    {
        var admin = await AdminAsync();
        var nueva = await admin.PostAsJsonAsync($"/api/v1/admin/tramites/tipos/{CatalogoTramites.ConstanciaExamenId}/plantillas", new
        {
            titulo = "Constancia de examen final",
            cuerpo = "Certificamos que {{nombreCompleto}} (DNI {{dni}}) rindió {{materia}} el {{fechaExamen}}. Emitida el {{fechaEmision}}.",
        });
        nueva.StatusCode.Should().Be(HttpStatusCode.Created);

        var (_, estudiante, tramite) = await PedirConstanciaAsync(
            CatalogoTramites.ConstanciaExamenId,
            new Dictionary<string, string?> { ["materia"] = "Didáctica General", ["fechaExamen"] = "2026-12-10" });
        await CargarDatosOficialesAsync(admin, tramite);
        var documento = (await LeerAsync<Detalle>(await AprobarAsync(admin, tramite.Id))).Documento!;

        documento.VersionPlantilla.Should().BeGreaterThan(1);
        documento.TipoDocumento.Should().Be("Constancia de examen final");
        using var leido = PdfDocument.Open(await (await estudiante.GetAsync($"/api/v1/tramites/{tramite.Id}/documento")).Content.ReadAsByteArrayAsync());
        string.Join(" ", leido.GetPages().Select(pagina => pagina.Text)).Should().Contain("rindió Didáctica General").And.Contain("10/12/2026");

        var plantillas = (await admin.GetFromJsonAsync<List<PlantillaDeTipo>>("/api/v1/admin/plantillas", Json))!;
        plantillas.Single(p => p.TipoId == CatalogoTramites.ConstanciaExamenId).Vigente!.Version.Should().Be(documento.VersionPlantilla);
        plantillas.Single(p => p.TipoId == CatalogoTramites.AnaliticoParcialId).Vigente.Should().BeNull();
    }

    [Fact]
    public async Task Una_plantilla_con_datos_inexistentes_se_rechaza()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/tramites/tipos/{CatalogoTramites.AlumnoRegularId}/plantillas", new { titulo = "X", cuerpo = "Promedio: {{promedio}}" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("{{promedio}}");
    }

    [Fact]
    public async Task Un_estudiante_no_puede_cargar_datos_oficiales_ni_plantillas()
    {
        var estudiante = await EstudianteAsync();

        (await estudiante.PutAsJsonAsync($"/api/v1/admin/usuarios/{Guid.NewGuid()}/datos-oficiales", new { nombre = "A", apellido = "B", dni = "30123456" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await estudiante.GetAsync("/api/v1/admin/plantillas")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void La_validacion_publica_tiene_limite_de_consultas()
    {
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(candidato => candidato.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "ValidarDocumento");

        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName.Should().Be("validacion-publica");
    }

    private sealed record IdDto(Guid Id);

    private sealed record Persona(Guid Id, string Email, string? Nombre, string? Apellido, string? Legajo, string? Dni);

    private sealed record Documento(Guid Id, string TipoDocumento, string CodigoValidacion, string UrlValidacion, string Estado, int VersionPlantilla, string HashSha256);

    private sealed record Detalle(Guid Id, string Numero, string Estado, Persona Solicitante, bool EmiteDocumento, Documento? Documento);

    private sealed record Validacion(string CodigoValidacion, string TipoDocumento, string NumeroTramite, DateTimeOffset FechaEmision, string Estado, string? DniParcial, string HashSha256);

    private sealed record Plantilla(Guid Id, int Version, string Titulo, string Cuerpo);

    private sealed record PlantillaDeTipo(Guid TipoId, string TipoNombre, Plantilla? Vigente);
}
