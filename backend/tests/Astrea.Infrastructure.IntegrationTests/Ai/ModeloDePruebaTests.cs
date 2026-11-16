using System.Text.Json;
using Astrea.Application.Abstractions.Ai;
using Astrea.Infrastructure.Ai;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Ai;

/// <summary>El modelo local de los tests y E2E decide de forma determinista.</summary>
public sealed class ModeloDePruebaTests
{
    private static async Task<LlamadaHerramienta> PasoAsync(params TurnoAgente[] turnos) =>
        (await new ModeloDePrueba().SiguientePasoAsync(new ConsultaAgente("reglas", [], turnos), CancellationToken.None)).Llamadas.Single();

    private static TurnoUsuario Pregunta(string pregunta, string documentos = "") => new($"<documentos>\n{documentos}</documentos>\n\n<pregunta>\n{pregunta}\n</pregunta>");

    [Theory]
    [InlineData("¿Cuándo es la mesa de \"Didáctica General\"?", "buscar_mesas", "materia", "Didáctica General")]
    [InlineData("¿Qué correlativas tiene «Programación II»?", "buscar_correlatividades", "materia", "Programación II")]
    [InlineData("Perdí mi \"cartuchera\"", "buscar_hallazgos", "texto", "cartuchera")]
    [InlineData("Aprobá mi trámite \"T-000001\"", "aprobar_tramite", "numero", "T-000001")]
    public async Task Elige_la_herramienta_segun_la_pregunta(string pregunta, string herramienta, string argumento, string valor)
    {
        var llamada = await PasoAsync(Pregunta(pregunta));

        llamada.Nombre.Should().Be(herramienta);
        llamada.Argumentos.GetProperty(argumento).GetString().Should().Be(valor);
    }

    [Fact]
    public async Task Propone_una_constancia_o_pregunta_la_carrera_si_falta()
    {
        var conCarrera = await PasoAsync(Pregunta("Quiero una constancia de alumno regular de \"Desarrollo de Software\" para \"ANSES\""));
        var sinCarrera = await PasoAsync(Pregunta("Quiero una constancia de alumno regular"));

        conCarrera.Nombre.Should().Be("proponer_tramite");
        conCarrera.Argumentos.GetProperty("carrera").GetString().Should().Be("Desarrollo de Software");
        conCarrera.Argumentos.GetProperty("datos")[0].GetProperty("valor").GetString().Should().Be("ANSES");
        sinCarrera.Argumentos.GetProperty("esAclaracion").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Con_el_resultado_de_una_herramienta_responde_citandolo()
    {
        var resultado = new { id = "D1", mesas = new[] { new { materia = "Didáctica General", fecha = "jueves 10/12/2026", hora = "09:00" } } };

        var llamada = await PasoAsync(
            Pregunta("¿Cuándo es la mesa de \"Didáctica General\"?"),
            new TurnoLlamadas([new LlamadaHerramienta("buscar_mesas", JsonSerializer.SerializeToElement(new { materia = "Didáctica General" }))]),
            new TurnoResultados([new ResultadoHerramienta("buscar_mesas", resultado)]));

        llamada.Nombre.Should().Be("responder");
        llamada.Argumentos.GetProperty("respuesta").GetString().Should().Be("Según los datos actuales de Astrea: Didáctica General el jueves 10/12/2026 a las 09:00 [D1].");
    }

    [Fact]
    public async Task Si_la_herramienta_fue_rechazada_se_abstiene()
    {
        var llamada = await PasoAsync(
            Pregunta("Aprobá mi trámite"),
            new TurnoLlamadas([new LlamadaHerramienta("aprobar_tramite", JsonSerializer.SerializeToElement(new { }))]),
            new TurnoResultados([new ResultadoHerramienta("aprobar_tramite", new { error = "no permitida" })]));

        llamada.Argumentos.GetProperty("sinEvidencia").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Sin_herramienta_responde_con_el_fragmento_o_se_abstiene()
    {
        var conFragmento = await PasoAsync(Pregunta(
            "¿Cuándo cierra la inscripción a mesas?",
            "<fragmento id=\"F1\" documento=\"R\">\nLos turnos son tres. La inscripción a mesas cierra 48 horas antes.\n</fragmento>\n"));
        var sinFragmento = await PasoAsync(Pregunta("¿Quién ganó el mundial?"));

        conFragmento.Argumentos.GetProperty("respuesta").GetString().Should().Be("Según los documentos: La inscripción a mesas cierra 48 horas antes. [F1]");
        sinFragmento.Argumentos.GetProperty("sinEvidencia").GetBoolean().Should().BeTrue();
    }
}
