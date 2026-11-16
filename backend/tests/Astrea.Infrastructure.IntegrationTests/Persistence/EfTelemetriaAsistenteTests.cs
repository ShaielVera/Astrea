using Astrea.Application.Features.Agent;
using Astrea.Domain.Agent;
using Astrea.Infrastructure.Persistence;
using Astrea.Infrastructure.Persistence.Repositories;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Persistence;

/// <summary>Agregaciones del panel de métricas en PostgreSQL. Cada test usa su propio año para no mezclarse con los demás.</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class EfTelemetriaAsistenteTests(BaseDeDatosFixture baseDeDatos)
{
    private static DateTimeOffset Mediodia(int anio, int mes, int dia) => new(anio, mes, dia, 15, 0, 0, TimeSpan.Zero);

    private async Task GuardarAsync(params ConsultaRegistrada[] consultas)
    {
        await using var db = baseDeDatos.CrearContexto();
        var telemetria = new EfTelemetriaAsistente(db);
        foreach (var consulta in consultas)
        {
            telemetria.Agregar(consulta);
        }

        await new EfUnitOfWork(db).GuardarCambiosAsync(CancellationToken.None);
    }

    private static ConsultaRegistrada Consulta(
        string pregunta,
        DateTimeOffset fecha,
        ResultadoConsulta resultado = ResultadoConsulta.Respondida,
        MotivoConsulta motivo = MotivoConsulta.Ninguno,
        string[]? herramientas = null,
        int duracionMs = 1000,
        bool conPropuesta = false) =>
        ConsultaRegistrada.Registrar(Guid.CreateVersion7(), pregunta, resultado, motivo, fecha, TimeSpan.FromMilliseconds(duracionMs), herramientas, conPropuesta: conPropuesta);

    private async Task<MetricasAsistenteDto> MetricasAsync(DateOnly desde, DateOnly hasta)
    {
        await using var db = baseDeDatos.CrearContexto();
        return await new EfTelemetriaAsistente(db).ObtenerMetricasAsync(desde, hasta, MetricasAsistente.MinimoFrecuencia, MetricasAsistente.MaximoTemas, CancellationToken.None);
    }

    [Fact]
    public async Task Agrega_por_resultado_por_dia_herramientas_y_motivos_del_periodo()
    {
        await GuardarAsync(
            Consulta("¿Cuándo cierra la inscripción?", Mediodia(2031, 3, 2), herramientas: ["buscar_mesas"], duracionMs: 1000),
            Consulta("cuando cierra la inscripcion", Mediodia(2031, 3, 2), herramientas: ["buscar_mesas", "buscar_correlatividades"], duracionMs: 3000),
            Consulta("Quiero una constancia", Mediodia(2031, 3, 3), herramientas: ["proponer_tramite"], conPropuesta: true, duracionMs: 2000),
            Consulta("¿Hay paro?", Mediodia(2031, 3, 3), ResultadoConsulta.SinEvidencia, MotivoConsulta.SinInformacion, duracionMs: 500),
            Consulta("¿Hay paro?", Mediodia(2031, 3, 4), ResultadoConsulta.SinEvidencia, MotivoConsulta.SinInformacion, duracionMs: 500),
            Consulta("¿De qué carrera?", Mediodia(2031, 3, 4), ResultadoConsulta.Aclaracion, duracionMs: 1000),
            Consulta("¿Cuándo cierra?", Mediodia(2031, 3, 4), ResultadoConsulta.Error, MotivoConsulta.ProveedorNoDisponible, duracionMs: 30000),
            // Fuera del período.
            Consulta("¿Cuándo cierra la inscripción?", Mediodia(2031, 4, 1)));

        var metricas = await MetricasAsync(new DateOnly(2031, 3, 1), new DateOnly(2031, 3, 31));

        metricas.Resumen.Should().Be(new ResumenConsultasDto(7, 3, 2, 1, 1, 1, 1333));
        metricas.PorDia.Should().Equal(
            new ConsultasDelDiaDto(new DateOnly(2031, 3, 2), 2, 0),
            new ConsultasDelDiaDto(new DateOnly(2031, 3, 3), 2, 1),
            new ConsultasDelDiaDto(new DateOnly(2031, 3, 4), 3, 2));
        metricas.Herramientas.Should().Equal(
            new CantidadDto("buscar_mesas", 2),
            new CantidadDto("buscar_correlatividades", 1),
            new CantidadDto("proponer_tramite", 1));
        metricas.Motivos.Should().Equal(new CantidadDto("SinInformacion", 2), new CantidadDto("ProveedorNoDisponible", 1));
    }

    [Fact]
    public async Task Los_temas_frecuentes_solo_muestran_los_repetidos_y_los_sin_respuesta_muestran_todo_lo_no_respondido()
    {
        await GuardarAsync(
            Consulta("¿Cuándo cierra la inscripción?", Mediodia(2032, 5, 1)),
            Consulta("cuando cierra la INSCRIPCION", Mediodia(2032, 5, 2)),
            Consulta("¿Cuándo cierra la inscripción?", Mediodia(2032, 5, 3)),
            Consulta("¿Qué correlativas tiene Programación II?", Mediodia(2032, 5, 1)),
            Consulta("que correlativas tiene programacion ii", Mediodia(2032, 5, 4)),
            Consulta("Una pregunta que hizo una sola persona", Mediodia(2032, 5, 1)),
            Consulta("¿Hay becas para el profesorado?", Mediodia(2032, 5, 2), ResultadoConsulta.SinEvidencia, MotivoConsulta.CitasInvalidas),
            Consulta("¿Hay paro?", Mediodia(2032, 5, 5), ResultadoConsulta.SinEvidencia, MotivoConsulta.SinInformacion),
            Consulta("¿Hay paro?", Mediodia(2032, 5, 6), ResultadoConsulta.SinEvidencia, MotivoConsulta.SinInformacion),
            Consulta("¿Cuándo cierra?", Mediodia(2032, 5, 6), ResultadoConsulta.Error, MotivoConsulta.ProveedorNoDisponible));

        var metricas = await MetricasAsync(new DateOnly(2032, 5, 1), new DateOnly(2032, 5, 31));

        metricas.Frecuentes.Should().Equal(
            new TemaDto("cuando cierra la inscripcion", 3, Mediodia(2032, 5, 3)),
            new TemaDto("hay paro", 2, Mediodia(2032, 5, 6)),
            new TemaDto("que correlativas tiene programacion ii", 2, Mediodia(2032, 5, 4)));

        // Los errores del proveedor no son temas sin respuesta: el asistente no llegó a buscar.
        metricas.SinRespuesta.Should().Equal(
            new TemaDto("hay paro", 2, Mediodia(2032, 5, 6)),
            new TemaDto("hay becas para el profesorado", 1, Mediodia(2032, 5, 2)));
    }

    [Fact]
    public async Task Un_periodo_sin_consultas_devuelve_todo_en_cero()
    {
        var metricas = await MetricasAsync(new DateOnly(1999, 1, 1), new DateOnly(1999, 1, 31));

        metricas.Resumen.Should().Be(new ResumenConsultasDto(0, 0, 0, 0, 0, 0, 0));
        metricas.PorDia.Should().BeEmpty();
        metricas.Frecuentes.Should().BeEmpty();
        metricas.Herramientas.Should().BeEmpty();
    }

    [Fact]
    public async Task La_depuracion_borra_solo_los_registros_anteriores_al_limite()
    {
        var vieja = Consulta("vieja", Mediodia(2033, 1, 1));
        var nueva = Consulta("nueva", Mediodia(2033, 6, 1));
        await GuardarAsync(vieja, nueva);

        await using (var db = baseDeDatos.CrearContexto())
        {
            (await new EfTelemetriaAsistente(db).EliminarAnterioresAsync(Mediodia(2033, 3, 1), CancellationToken.None)).Should().BeGreaterThanOrEqualTo(1);
        }

        await using var lectura = baseDeDatos.CrearContexto();
        lectura.ConsultasRegistradas.Any(consulta => consulta.Id == vieja.Id).Should().BeFalse();
        lectura.ConsultasRegistradas.Any(consulta => consulta.Id == nueva.Id).Should().BeTrue();
    }
}
