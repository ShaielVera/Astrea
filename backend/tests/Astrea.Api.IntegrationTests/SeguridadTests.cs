using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Astrea.Api.IntegrationTests;

/// <summary>
/// Auditoría de permisos sobre TODOS los endpoints (un endpoint nuevo mal protegido hace fallar el test) y endurecimiento
/// de las respuestas: cabeceras de seguridad, nada de caché, id de correlación y límite de pedidos.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed partial class SeguridadTests(AstreaApiFactory factory)
{
    /// <summary>Los únicos endpoints de la API que se pueden usar sin sesión.</summary>
    private static readonly string[] Anonimos =
    [
        "GET /api/v1/",
        "GET /api/v1/documentos/validar/{codigo}",
    ];

    private sealed record Ruta(string Metodo, string Patron, bool Anonimo, IReadOnlyList<string> Politicas)
    {
        public string Clave => $"{Metodo} {Patron}";

        /// <summary>El patrón con valores de ejemplo en los parámetros: {id:guid} → un guid nuevo, {codigo} → "x".</summary>
        public string Url() => Parametro().Replace(Patron, parametro => parametro.Value.Contains(":guid", StringComparison.Ordinal) ? Guid.NewGuid().ToString() : "x");
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex Parametro();

    private List<Ruta> RutasDeLaApi() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1", StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"]).Select(metodo => new Ruta(
                metodo,
                endpoint.RoutePattern.RawText!,
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(dato => dato.Policy).OfType<string>().ToList())))
            .ToList();

    private async Task<HttpClient> SesionAsync(string email)
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", email));
        (await client.PostAsync("/api/v1/auth/sesion", null)).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public void Solo_la_validacion_publica_y_la_informacion_de_la_api_se_pueden_usar_sin_sesion()
    {
        var rutas = RutasDeLaApi();

        rutas.Should().HaveCountGreaterThan(40, "el test tiene que estar viendo todos los endpoints");
        rutas.Where(ruta => ruta.Anonimo).Select(ruta => ruta.Clave).Should().BeEquivalentTo(Anonimos);
    }

    [Fact]
    public void Todo_endpoint_de_administracion_exige_el_rol_de_administrador()
    {
        var administracion = RutasDeLaApi().Where(ruta => ruta.Patron.StartsWith("/api/v1/admin", StringComparison.Ordinal)).ToList();

        administracion.Should().NotBeEmpty();
        administracion.Where(ruta => !ruta.Politicas.Contains("Administrador")).Select(ruta => ruta.Clave).Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_sesion_ningun_endpoint_protegido_responde_datos()
    {
        var anonimo = factory.CreateClient();
        foreach (var ruta in RutasDeLaApi().Where(ruta => !ruta.Anonimo))
        {
            var respuesta = await anonimo.SendAsync(new HttpRequestMessage(new HttpMethod(ruta.Metodo), ruta.Url()));
            respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized, ruta.Clave);
        }
    }

    [Fact]
    public async Task Un_estudiante_recibe_403_en_todos_los_endpoints_de_administracion()
    {
        var estudiante = await SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");
        foreach (var ruta in RutasDeLaApi().Where(ruta => ruta.Patron.StartsWith("/api/v1/admin", StringComparison.Ordinal)))
        {
            var respuesta = await estudiante.SendAsync(new HttpRequestMessage(new HttpMethod(ruta.Metodo), ruta.Url()));
            respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden, ruta.Clave);
        }
    }

    [Fact]
    public async Task Ningun_listado_devuelve_mas_de_50_elementos_aunque_se_pidan_mas()
    {
        var administrador = await SesionAsync(AstreaApiFactory.AdministradorInicial);
        var estudiante = await SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");
        var paginados = new List<string>();

        foreach (var ruta in RutasDeLaApi().Where(ruta => ruta.Metodo == "GET" && !ruta.Patron.Contains('{', StringComparison.Ordinal)))
        {
            var cliente = ruta.Patron.StartsWith("/api/v1/admin", StringComparison.Ordinal) ? administrador : estudiante;
            var respuesta = await cliente.GetAsync($"{ruta.Patron}?page=1&pageSize=100000");
            if (!respuesta.IsSuccessStatusCode || respuesta.Content.Headers.ContentType?.MediaType != "application/json")
            {
                continue;
            }

            using var json = System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            if (json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && json.RootElement.TryGetProperty("pageSize", out var tamanio))
            {
                paginados.Add(ruta.Clave);
                tamanio.GetInt32().Should().BeLessThanOrEqualTo(50, ruta.Clave);
                json.RootElement.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(50, ruta.Clave);
            }
        }

        paginados.Should().HaveCountGreaterThanOrEqualTo(6, "el test tiene que estar revisando los listados");
    }

    [Fact]
    public async Task Las_respuestas_llevan_cabeceras_de_seguridad_y_no_se_guardan_en_cache()
    {
        var estudiante = await SesionAsync($"alumno-{Guid.NewGuid():N}@astrea.test");

        var respuesta = await estudiante.GetAsync("/api/v1/me");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        respuesta.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        respuesta.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        respuesta.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'").And.Contain("frame-ancestors 'none'");
        respuesta.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Cada_pedido_tiene_un_id_de_correlacion_que_tambien_aparece_en_los_errores()
    {
        var anonimo = factory.CreateClient();

        var generado = await anonimo.GetAsync("/api/v1/documentos/validar/NO-EXISTE");
        var id = generado.Headers.GetValues("X-Correlation-Id").Single();
        id.Should().MatchRegex("^[0-9a-f]{32}$");
        (await generado.Content.ReadFromJsonAsync<Problema>())!.CorrelationId.Should().Be(id);

        var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v1/");
        pedido.Headers.Add("X-Correlation-Id", "pedido-del-frontend-123");
        (await anonimo.SendAsync(pedido)).Headers.GetValues("X-Correlation-Id").Should().Equal("pedido-del-frontend-123");

        // Un valor que no es seguro para los logs se reemplaza.
        var malicioso = new HttpRequestMessage(HttpMethod.Get, "/api/v1/");
        malicioso.Headers.TryAddWithoutValidation("X-Correlation-Id", "x\" inyectado <script>");
        (await anonimo.SendAsync(malicioso)).Headers.GetValues("X-Correlation-Id").Single().Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task Iniciar_sesion_demasiadas_veces_seguidas_devuelve_429_con_un_mensaje_claro()
    {
        var client = factory.CrearClienteCon(TokensDePrueba.Crear($"uid-{Guid.NewGuid():N}", $"alumno-{Guid.NewGuid():N}@astrea.test"));

        HttpResponseMessage respuesta;
        var intentos = 0;
        do
        {
            respuesta = await client.PostAsync("/api/v1/auth/sesion", null);
            intentos++;
        }
        while (respuesta.StatusCode == HttpStatusCode.OK && intentos < 50);

        respuesta.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        intentos.Should().Be(21);
        var problema = (await respuesta.Content.ReadFromJsonAsync<Problema>())!;
        problema.Detail.Should().Contain("Esperá un momento");
        problema.Code.Should().Be("demasiados_pedidos");
    }

    private sealed record Problema(string? Detail, string? Code, string? CorrelationId);
}
