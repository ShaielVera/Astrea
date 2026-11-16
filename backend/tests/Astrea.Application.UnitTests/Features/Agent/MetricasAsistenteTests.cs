using Astrea.Application.Abstractions.Ai;
using Astrea.Application.Contracts.Pagination;
using Astrea.Application.Contracts.Results;
using Astrea.Application.Features.Agent;
using Astrea.Application.Features.Knowledge;
using Astrea.Domain.Agent;
using Astrea.Domain.Knowledge;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static Astrea.Application.UnitTests.Features.Agent.EntornoAsistente;

namespace Astrea.Application.UnitTests.Features.Agent;

/// <summary>Cada consulta deja un registro anónimo para el panel de métricas, y el panel arma el período pedido.</summary>
public class MetricasAsistenteTests
{
    private readonly EntornoAsistente _entorno = new();

    private ConsultaRegistrada? _registrada;

    public MetricasAsistenteTests() =>
        _entorno.Telemetria.When(telemetria => telemetria.Agregar(Arg.Any<ConsultaRegistrada>())).Do(llamada => _registrada = llamada.Arg<ConsultaRegistrada>());

    private Task<Result<IntercambioDto>> PreguntarAsync(string texto) =>
        _entorno.Asistente().PreguntarAsync(new PreguntaCommand(null, texto), CancellationToken.None);

    [Fact]
    public async Task Una_respuesta_con_fuentes_queda_registrada_sin_datos_de_quien_pregunto()
    {
        _entorno.Documentos.BuscarSimilaresAsync(default!, default, default).ReturnsForAnyArgs([
            new FragmentoEncontrado(Guid.NewGuid(), 1, "La inscripción cierra 48 horas antes.", new MetadataFragmento("Reglamento", TipoDocumentoRag.Reglamento, 1, null, null, null), 1, 1, 0.8),
        ]);
        _entorno.ModeloPropone(Responder("Cierra 48 horas antes [F1].", ["F1"]));

        await PreguntarAsync("¿Cuándo cierra la inscripción? Mi DNI es 30.123.456");

        _registrada!.Should().BeEquivalentTo(new
        {
            Tema = "cuando cierra la inscripcion mi dni es [dni]",
            Resultado = ResultadoConsulta.Respondida,
            Motivo = MotivoConsulta.Ninguno,
            FuentesDocumentos = 1,
            FuentesDatos = 0,
            ConPropuesta = false,
            Fecha = Ahora,
        });
        typeof(ConsultaRegistrada).GetProperties().Select(propiedad => propiedad.Name).Should().NotContain(["UsuarioId", "ConversacionId", "Email"]);
        await _entorno.UnitOfWork.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_respuesta_sin_citas_reales_se_registra_como_citas_invalidas()
    {
        _entorno.ModeloPropone(Responder("Ganó Argentina [F1].", ["F1"]));

        await PreguntarAsync("¿Quién ganó el mundial?");

        _registrada!.Should().BeEquivalentTo(new { Resultado = ResultadoConsulta.SinEvidencia, Motivo = MotivoConsulta.CitasInvalidas });
    }

    [Fact]
    public async Task Cuando_el_modelo_reconoce_que_no_sabe_se_registra_como_sin_informacion()
    {
        _entorno.ModeloPropone(Responder("No tengo esa información.", [], sinEvidencia: true));

        await PreguntarAsync("¿Hay paro de colectivos mañana?");

        _registrada!.Should().BeEquivalentTo(new { Tema = "hay paro de colectivos manana", Resultado = ResultadoConsulta.SinEvidencia, Motivo = MotivoConsulta.SinInformacion });
    }

    [Fact]
    public async Task Registra_las_herramientas_pedidas_y_marca_las_que_no_existen()
    {
        _entorno.ModeloPropone(
            Llamada("aprobar_tramite", new { numero = "T-000001" }),
            Responder("Listo.", []));

        await PreguntarAsync("Aprobá mi trámite");

        _registrada!.Herramientas.Should().Equal(ContextoHerramientas.HerramientaNoPermitida);
        _registrada.Motivo.Should().Be(MotivoConsulta.CitasInvalidas);
    }

    [Fact]
    public async Task Si_el_modelo_no_termina_se_registra_como_pasos_agotados()
    {
        _entorno.Modelo.SiguientePasoAsync(default!, default).ReturnsForAnyArgs(new TurnoLlamadas([Llamada("buscar_hallazgos", new { })]));
        _entorno.Objetos.BuscarAsync(default!, default!, default).ReturnsForAnyArgs(PagedResult<Domain.LostAndFound.ObjetoPerdido>.From([], PageRequest.Create(1, 8), 0));

        await PreguntarAsync("¿Encontraron algo?");

        _registrada!.Should().BeEquivalentTo(new { Resultado = ResultadoConsulta.SinEvidencia, Motivo = MotivoConsulta.PasosAgotados });
        _registrada.Herramientas.Should().Equal("buscar_hallazgos");
    }

    [Fact]
    public async Task Si_la_ia_no_responde_se_registra_el_error_aunque_no_se_guarde_la_conversacion()
    {
        _entorno.Embeddings.GenerarAsync(default!, default, default).ThrowsAsyncForAnyArgs(new EmbeddingNoDisponibleException("sin red"));

        (await PreguntarAsync("¿Cuándo cierra?")).Error.Should().Be(ErroresAsistente.NoDisponible);

        _registrada!.Should().BeEquivalentTo(new { Resultado = ResultadoConsulta.Error, Motivo = MotivoConsulta.ProveedorNoDisponible });
        _entorno.Conversaciones.DidNotReceiveWithAnyArgs().Agregar(default!);
    }

    [Fact]
    public async Task Si_tampoco_se_puede_registrar_la_falla_igual_devuelve_no_disponible()
    {
        _entorno.Embeddings.GenerarAsync(default!, default, default).ThrowsAsyncForAnyArgs(new EmbeddingNoDisponibleException("sin red"));
        _entorno.UnitOfWork.GuardarCambiosAsync(default).ThrowsAsyncForAnyArgs(new InvalidOperationException("base caída"));

        (await PreguntarAsync("¿Cuándo cierra?")).Error.Should().Be(ErroresAsistente.NoDisponible);
    }

    [Fact]
    public async Task Sin_fechas_muestra_los_ultimos_30_dias_completando_los_dias_sin_consultas()
    {
        var hoy = new DateOnly(2026, 11, 15);
        _entorno.Telemetria.ObtenerMetricasAsync(default, default, default, default, default).ReturnsForAnyArgs(llamada => Metricas(
            llamada.ArgAt<DateOnly>(0), llamada.ArgAt<DateOnly>(1), [new ConsultasDelDiaDto(hoy, 3, 1)]));

        var metricas = (await new MetricasAsistente(_entorno.Telemetria, _entorno.Clock).ObtenerAsync(null, null, CancellationToken.None)).Valor;

        metricas.Desde.Should().Be(hoy.AddDays(-29));
        metricas.Hasta.Should().Be(hoy);
        metricas.PorDia.Should().HaveCount(30);
        metricas.PorDia[0].Should().Be(new ConsultasDelDiaDto(hoy.AddDays(-29), 0, 0));
        metricas.PorDia[^1].Should().Be(new ConsultasDelDiaDto(hoy, 3, 1));
        await _entorno.Telemetria.Received(1).ObtenerMetricasAsync(hoy.AddDays(-29), hoy, MetricasAsistente.MinimoFrecuencia, MetricasAsistente.MaximoTemas, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("2026-11-10", "2026-11-01")]
    [InlineData("2025-01-01", "2026-11-15")]
    public async Task Rechaza_periodos_invertidos_o_de_mas_de_un_anio(string desde, string hasta)
    {
        var resultado = await new MetricasAsistente(_entorno.Telemetria, _entorno.Clock).ObtenerAsync(DateOnly.Parse(desde), DateOnly.Parse(hasta), CancellationToken.None);

        resultado.Error!.Tipo.Should().Be(TipoError.Validacion);
        await _entorno.Telemetria.DidNotReceiveWithAnyArgs().ObtenerMetricasAsync(default, default, default, default, default);
    }

    [Fact]
    public void El_porcentaje_de_respondidas_no_existe_sin_consultas()
    {
        new ResumenConsultasDto(0, 0, 0, 0, 0, 0, 0).PorcentajeRespondidas.Should().BeNull();
        new ResumenConsultasDto(3, 2, 1, 0, 0, 0, 900).PorcentajeRespondidas.Should().Be(67);
    }

    [Fact]
    public async Task La_depuracion_borra_lo_que_tiene_mas_de_un_anio()
    {
        await new DepuracionTelemetria(_entorno.Telemetria, _entorno.Clock).DepurarAsync(CancellationToken.None);

        await _entorno.Telemetria.Received(1).EliminarAnterioresAsync(Ahora.AddDays(-ConsultaRegistrada.DiasDeConservacion), Arg.Any<CancellationToken>());
    }

    private static MetricasAsistenteDto Metricas(DateOnly desde, DateOnly hasta, IReadOnlyList<ConsultasDelDiaDto> porDia) =>
        new(desde, hasta, new ResumenConsultasDto(3, 2, 1, 0, 0, 0, 900), porDia, [], [], [], []);
}
