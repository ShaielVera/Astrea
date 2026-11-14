using System.Net;
using System.Net.Http.Json;
using Astrea.Api.IntegrationTests.Infrastructure;
using AwesomeAssertions;

namespace Astrea.Api.IntegrationTests;

public sealed class HealthTests(AstreaApiFactory factory) : IClassFixture<AstreaApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Los_health_checks_responden_Healthy(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task La_raiz_de_la_api_v1_responde_con_su_version()
    {
        var info = await _client.GetFromJsonAsync<ApiInfo>("/api/v1/");

        info.Should().Be(new ApiInfo("Astrea API", "v1"));
    }

    [Fact]
    public async Task Una_ruta_inexistente_devuelve_404_con_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/no-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    private sealed record ApiInfo(string Name, string Version);
}
