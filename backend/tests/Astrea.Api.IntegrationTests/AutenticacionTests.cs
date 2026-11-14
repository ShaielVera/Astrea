using System.Net;
using System.Net.Http.Headers;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

/// <summary>La API rechaza todo token que no sea un Firebase ID Token válido de este proyecto.</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class AutenticacionTests(AstreaApiFactory factory)
{
    private const string Uid = "uid-autenticacion";
    private const string Email = "alumna@astrea.test";

    private async Task<HttpResponseMessage> GetMeCon(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.GetAsync("/api/v1/me");
    }

    [Fact]
    public async Task Sin_token_devuelve_401()
    {
        (await GetMeCon(null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_que_no_es_un_jwt_devuelve_401()
    {
        (await GetMeCon("esto-no-es-un-token")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_firmado_con_otra_clave_devuelve_401()
    {
        var token = TokensDePrueba.Crear(Uid, Email, clave: TokensDePrueba.ClaveAjena);

        (await GetMeCon(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_de_otro_proyecto_de_firebase_devuelve_401()
    {
        var token = TokensDePrueba.Crear(Uid, Email, projectId: "otro-proyecto");

        (await GetMeCon(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_vencido_devuelve_401()
    {
        var token = TokensDePrueba.Crear(Uid, Email, vence: DateTime.UtcNow.AddMinutes(-10));

        (await GetMeCon(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_sin_firma_no_se_acepta_fuera_del_emulador()
    {
        var partes = TokensDePrueba.Crear(Uid, Email).Split('.');
        var sinFirma = $"{Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{partes[1]}.";

        (await GetMeCon(sinFirma)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string Base64Url(string texto) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
