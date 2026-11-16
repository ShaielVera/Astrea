using System.Net;
using System.Text;

namespace Astrea.Infrastructure.IntegrationTests.Ai;

/// <summary>Simula la API de Gemini: guarda los pedidos y devuelve las respuestas encoladas, en orden.</summary>
internal sealed class ServidorSimulado : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Codigo, string Cuerpo)> _respuestas = new();

    public List<(string Url, string? ApiKey, string Cuerpo)> Pedidos { get; } = [];

    public void Responder(HttpStatusCode codigo, string cuerpo) => _respuestas.Enqueue((codigo, cuerpo));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Pedidos.Add((
            request.RequestUri!.ToString(),
            request.Headers.TryGetValues("x-goog-api-key", out var valores) ? valores.Single() : null,
            await request.Content!.ReadAsStringAsync(cancellationToken)));
        var (codigo, cuerpo) = _respuestas.Dequeue();
        return new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
    }
}
