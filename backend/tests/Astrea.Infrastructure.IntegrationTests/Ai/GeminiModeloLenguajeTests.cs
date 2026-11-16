using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Astrea.Application.Abstractions.Ai;
using Astrea.Infrastructure.Ai;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Ai;

/// <summary>El modelo del asistente (function calling) contra un servidor simulado de Gemini: no sale a internet ni gasta cuota.</summary>
public sealed class GeminiModeloLenguajeTests
{
    private static readonly DefinicionHerramienta BuscarMesas = new("buscar_mesas", "Mesas de examen.",
    [
        new("materia", TipoParametro.Texto, "Materia.", true),
        new("desde", TipoParametro.Fecha, "Desde.", false),
    ]);

    private static readonly DefinicionHerramienta Proponer = new("proponer_tramite", "Propone un trámite.",
    [
        new("tipo", TipoParametro.Texto, "Tipo.", true, ["ALUMNO_REGULAR", "ANALITICO_PARCIAL"]),
        new("datos", TipoParametro.ListaDeObjetos, "Datos.", false, Campos: [new("clave", TipoParametro.Texto, "Clave.", true), new("valor", TipoParametro.Texto, "Valor.", true)]),
        new("urgente", TipoParametro.Booleano, "Urgente.", false),
        new("fuentes", TipoParametro.ListaDeTextos, "Fuentes.", false),
    ]);

    private static readonly ConsultaAgente Consulta = new(
        "Reglas del sistema",
        [BuscarMesas, Proponer],
        [new TurnoUsuario("¿Cuándo cierra la inscripción?"), new TurnoModelo("Cierra 48 horas antes [1]."), new TurnoUsuario("<pregunta>¿Y la mesa de Didáctica?</pregunta>")]);

    private readonly ServidorSimulado _servidor = new();

    private GeminiModeloLenguaje Modelo(string? apiKey = "clave-de-prueba", string? nivel = "low") =>
        new(new HttpClient(_servidor), new ConfiguracionGemini { ApiKey = apiKey, UrlBase = "https://gemini.test/v1beta/", ModeloChat = "gemini-de-prueba", NivelRazonamiento = nivel })
        {
            Espera = _ => TimeSpan.Zero,
        };

    /// <summary>Una respuesta de Gemini con llamadas a funciones (y la firma de razonamiento que hay que devolver tal cual).</summary>
    private static string Llamadas(params object[] llamadas) => JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new
            {
                content = new { role = "model", parts = llamadas.Select(args => (object)new { functionCall = args, thoughtSignature = "firma-opaca" }).ToArray() },
                finishReason = "STOP",
            },
        },
    });

    [Fact]
    public async Task Envia_instrucciones_historial_y_herramientas_en_modo_any()
    {
        _servidor.Responder(HttpStatusCode.OK, Llamadas(new { name = "buscar_mesas", args = new { materia = "Didáctica" } }));

        var paso = await Modelo().SiguientePasoAsync(Consulta, CancellationToken.None);

        paso.Llamadas.Should().ContainSingle();
        paso.Llamadas[0].Nombre.Should().Be("buscar_mesas");
        paso.Llamadas[0].Argumentos.GetProperty("materia").GetString().Should().Be("Didáctica");

        var pedido = _servidor.Pedidos.Single();
        pedido.Url.Should().Be("https://gemini.test/v1beta/models/gemini-de-prueba:generateContent");
        pedido.ApiKey.Should().Be("clave-de-prueba");
        var cuerpo = JsonNode.Parse(pedido.Cuerpo)!;
        cuerpo["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>().Should().Be("Reglas del sistema");
        cuerpo["contents"]!.AsArray().Select(contenido => contenido!["role"]!.GetValue<string>()).Should().Equal("user", "model", "user");
        cuerpo["toolConfig"]!["functionCallingConfig"]!["mode"]!.GetValue<string>().Should().Be("ANY");
        cuerpo["generationConfig"]!["thinkingConfig"]!["thinkingLevel"]!.GetValue<string>().Should().Be("low");

        var declaraciones = cuerpo["tools"]![0]!["functionDeclarations"]!.AsArray();
        declaraciones.Select(declaracion => declaracion!["name"]!.GetValue<string>()).Should().Equal("buscar_mesas", "proponer_tramite");
        declaraciones[0]!["parameters"]!["required"]!.AsArray().Select(r => r!.GetValue<string>()).Should().Equal("materia");
        var proponer = declaraciones[1]!["parameters"]!["properties"]!;
        proponer["tipo"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()).Should().Equal("ALUMNO_REGULAR", "ANALITICO_PARCIAL");
        proponer["datos"]!["items"]!["properties"]!["valor"]!["type"]!.GetValue<string>().Should().Be("STRING");
        proponer["urgente"]!["type"]!.GetValue<string>().Should().Be("BOOLEAN");
        proponer["fuentes"]!["items"]!["type"]!.GetValue<string>().Should().Be("STRING");
    }

    [Fact]
    public async Task Devuelve_el_turno_del_modelo_tal_cual_y_los_resultados_de_las_herramientas()
    {
        _servidor.Responder(HttpStatusCode.OK, Llamadas(new { name = "buscar_mesas", args = new { materia = "Didáctica" } }));
        _servidor.Responder(HttpStatusCode.OK, Llamadas(new { name = "responder", args = new { respuesta = "Es el 10/12 [D1].", fuentes = new[] { "D1" } } }));

        var primero = await Modelo().SiguientePasoAsync(Consulta, CancellationToken.None);
        var conResultados = Consulta with
        {
            Turnos = [.. Consulta.Turnos, primero, new TurnoResultados([new ResultadoHerramienta("buscar_mesas", new { id = "D1", total = 1 })])],
        };
        var segundo = await Modelo().SiguientePasoAsync(conResultados, CancellationToken.None);

        segundo.Llamadas.Single().Nombre.Should().Be("responder");
        var contenidos = JsonNode.Parse(_servidor.Pedidos[1].Cuerpo)!["contents"]!.AsArray();
        contenidos[3]!["parts"]![0]!["thoughtSignature"]!.GetValue<string>().Should().Be("firma-opaca", "Gemini exige recibir la firma sin cambios");
        contenidos[3]!["parts"]![0]!["functionCall"]!["name"]!.GetValue<string>().Should().Be("buscar_mesas");
        var respuesta = contenidos[4]!["parts"]![0]!["functionResponse"]!;
        respuesta["name"]!.GetValue<string>().Should().Be("buscar_mesas");
        respuesta["response"]!["resultado"]!["id"]!.GetValue<string>().Should().Be("D1");
        contenidos[4]!["role"]!.GetValue<string>().Should().Be("user");
    }

    [Fact]
    public async Task Varias_llamadas_en_un_paso_se_devuelven_todas()
    {
        _servidor.Responder(HttpStatusCode.OK, Llamadas(new { name = "buscar_mesas", args = new { materia = "A" } }, new { name = "buscar_mesas", args = new { materia = "B" } }));

        var paso = await Modelo().SiguientePasoAsync(Consulta, CancellationToken.None);

        paso.Llamadas.Select(llamada => llamada.Argumentos.GetProperty("materia").GetString()).Should().Equal("A", "B");
    }

    [Fact]
    public async Task Sin_nivel_de_razonamiento_no_envia_esa_configuracion()
    {
        _servidor.Responder(HttpStatusCode.OK, Llamadas(new { name = "responder", args = new { respuesta = "Hola" } }));

        await Modelo(nivel: null).SiguientePasoAsync(Consulta, CancellationToken.None);

        JsonNode.Parse(_servidor.Pedidos.Single().Cuerpo)!["generationConfig"]!["thinkingConfig"].Should().BeNull();
    }

    [Theory]
    [InlineData("""{"promptFeedback":{"blockReason":"PROHIBITED_CONTENT"}}""")]
    [InlineData("""{"candidates":[{"finishReason":"SAFETY"}]}""")]
    [InlineData("""{"candidates":[{"content":{"role":"model","parts":[{"text":"Respuesta suelta sin herramienta"}]},"finishReason":"STOP"}]}""")]
    public async Task Si_gemini_bloquea_o_no_llama_a_ninguna_herramienta_no_hay_llamadas(string cuerpo)
    {
        _servidor.Responder(HttpStatusCode.OK, cuerpo);

        (await Modelo().SiguientePasoAsync(Consulta, CancellationToken.None)).Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Reintenta_una_vez_ante_un_error_temporal()
    {
        _servidor.Responder(HttpStatusCode.ServiceUnavailable, """{"error":{"code":503,"message":"overloaded"}}""");
        _servidor.Responder(HttpStatusCode.OK, Llamadas(new { name = "responder", args = new { respuesta = "Hola" } }));

        (await Modelo().SiguientePasoAsync(Consulta, CancellationToken.None)).Llamadas.Should().ContainSingle();
        _servidor.Pedidos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Si_sigue_fallando_avisa_que_no_esta_disponible()
    {
        _servidor.Responder(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"message":"quota"}}""");
        _servidor.Responder(HttpStatusCode.TooManyRequests, """{"error":{"code":429,"message":"quota"}}""");

        var paso = () => Modelo().SiguientePasoAsync(Consulta, CancellationToken.None);

        await paso.Should().ThrowAsync<ModeloNoDisponibleException>().WithMessage("*429*");
        _servidor.Pedidos.Should().HaveCount(2, "con alguien esperando, reintenta una sola vez");
    }

    [Fact]
    public async Task Una_api_key_rechazada_o_faltante_avisa_que_no_esta_disponible()
    {
        _servidor.Responder(HttpStatusCode.BadRequest, """{"error":{"code":400,"message":"API key not valid. Please pass a valid API key."}}""");

        await FluentActions.Awaiting(() => Modelo().SiguientePasoAsync(Consulta, CancellationToken.None)).Should().ThrowAsync<ModeloNoDisponibleException>().WithMessage("*rechazó la API key*");
        await FluentActions.Awaiting(() => Modelo(apiKey: null).SiguientePasoAsync(Consulta, CancellationToken.None)).Should().ThrowAsync<ModeloNoDisponibleException>().WithMessage("*Falta configurar*");
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("""{"candidates":[{"finishReason":"MAX_TOKENS"}]}""")]
    public async Task Una_respuesta_invalida_avisa_que_no_esta_disponible(string cuerpo)
    {
        _servidor.Responder(HttpStatusCode.OK, cuerpo);

        await FluentActions.Awaiting(() => Modelo().SiguientePasoAsync(Consulta, CancellationToken.None)).Should().ThrowAsync<ModeloNoDisponibleException>();
    }
}
