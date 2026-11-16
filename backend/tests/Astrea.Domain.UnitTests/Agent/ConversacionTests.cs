using Astrea.Domain.Agent;
using Astrea.Domain.Common;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Agent;

public class ConversacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Estudiante = Guid.NewGuid();

    private static readonly FuenteRespuesta Fuente =
        new(Guid.NewGuid(), "Reglamento Académico", 1, "CAPÍTULO I", "Resolución 4043/09", 1, 1, "La inscripción cierra 48 horas antes.");

    private static Conversacion Nueva(string pregunta = "¿Cuándo cierra la inscripción a mesas?") =>
        Conversacion.Iniciar(Guid.NewGuid(), Estudiante, pregunta, Ahora);

    [Fact]
    public void El_titulo_es_el_comienzo_de_la_primera_pregunta()
    {
        Nueva("  ¿Cuándo   cierra la\ninscripción?  ").Titulo.Should().Be("¿Cuándo cierra la inscripción?");

        var larga = Nueva(new string('a', 200));
        larga.Titulo.Should().HaveLength(Conversacion.LargoMaximoTitulo).And.EndWith("…");
    }

    [Fact]
    public void Registrar_guarda_la_pregunta_y_la_respuesta_con_sus_fuentes()
    {
        var conversacion = Nueva();

        var (pregunta, respuesta) = conversacion.Registrar("¿Cuándo cierra?", "Cierra 48 horas antes [1].", [Fuente], TipoRespuesta.Respuesta, Ahora.AddMinutes(1));

        pregunta.Rol.Should().Be(RolMensaje.Usuario);
        respuesta.Rol.Should().Be(RolMensaje.Asistente);
        respuesta.Fuentes.Should().ContainSingle().Which.Should().Be(Fuente);
        conversacion.Mensajes.Should().HaveCount(2);
        conversacion.CantidadDePreguntas.Should().Be(1);
        conversacion.FechaUltimoMensaje.Should().Be(Ahora.AddMinutes(1));
        conversacion.UltimaPregunta.Should().Be("¿Cuándo cierra?");
    }

    [Fact]
    public void Una_respuesta_oficial_sin_fuentes_no_se_acepta_y_una_abstencion_no_cita_fuentes()
    {
        var conversacion = Nueva();

        var sinFuentes = () => conversacion.Registrar("¿Cuándo cierra?", "Cierra 48 horas antes.", [], TipoRespuesta.Respuesta, Ahora);
        var abstencionConFuentes = () => conversacion.Registrar("¿Cuándo cierra?", "No sé.", [Fuente], TipoRespuesta.SinEvidencia, Ahora);

        sinFuentes.Should().Throw<DomainException>().WithMessage("*al menos una fuente*");
        abstencionConFuentes.Should().Throw<DomainException>();
        conversacion.Registrar("¿Y el fútbol?", "No encontré información.", [], TipoRespuesta.SinEvidencia, Ahora).Respuesta.SinEvidencia.Should().BeTrue();
    }

    [Fact]
    public void Tiene_un_maximo_de_preguntas()
    {
        var conversacion = Nueva();
        for (var i = 0; i < Conversacion.PreguntasMaximas; i++)
        {
            conversacion.Registrar($"Pregunta {i}", "No encontré información.", [], TipoRespuesta.SinEvidencia, Ahora.AddSeconds(i));
        }

        var otra = () => conversacion.Registrar("Una más", "No encontré información.", [], TipoRespuesta.SinEvidencia, Ahora);

        otra.Should().Throw<DomainException>().WithMessage("*Empezá una nueva*");
    }

    [Fact]
    public void Los_ultimos_mensajes_salen_en_orden_cronologico()
    {
        var conversacion = Nueva();
        conversacion.Registrar("Primera", "R1", [Fuente], TipoRespuesta.Respuesta, Ahora.AddMinutes(1));
        conversacion.Registrar("Segunda", "R2", [Fuente], TipoRespuesta.Respuesta, Ahora.AddMinutes(2));

        conversacion.UltimosMensajes(3).Select(mensaje => mensaje.Contenido).Should().Equal("R1", "Segunda", "R2");
        conversacion.UltimaPregunta.Should().Be("Segunda");
    }

    [Fact]
    public void Solo_pertenece_a_quien_la_inicio_y_se_depura_a_los_30_dias_sin_actividad()
    {
        var conversacion = Nueva();

        conversacion.PerteneceA(Estudiante).Should().BeTrue();
        conversacion.PerteneceA(Guid.NewGuid()).Should().BeFalse();
        conversacion.DebeDepurarse(Ahora.AddDays(29)).Should().BeFalse();
        conversacion.DebeDepurarse(Ahora.AddDays(Conversacion.DiasDeConservacion)).Should().BeTrue();
    }

    private static PropuestaTramite Propuesta() =>
        new(Guid.NewGuid(), "Constancia de alumno regular", new Dictionary<string, string?> { ["presentarAnte"] = "ANSES" }, [new DatoPropuesta("Para presentar ante", "ANSES")]);

    [Fact]
    public void Una_propuesta_de_tramite_se_confirma_una_sola_vez()
    {
        var conversacion = Nueva();
        var (_, respuesta) = conversacion.Registrar("Quiero una constancia", "Te preparé la solicitud.", [], TipoRespuesta.Respuesta, Ahora, Propuesta());

        respuesta.Propuesta!.Estado.Should().Be(EstadoPropuesta.Pendiente);
        var tramiteId = Guid.NewGuid();
        conversacion.ConfirmarPropuesta(respuesta.Id, tramiteId, "T-000007", Ahora.AddMinutes(1)).Propuesta.Should()
            .BeEquivalentTo(new { Estado = EstadoPropuesta.Confirmada, TramiteId = tramiteId, NumeroTramite = "T-000007" });

        var otraVez = () => conversacion.ConfirmarPropuesta(respuesta.Id, Guid.NewGuid(), "T-000008", Ahora.AddMinutes(2));
        var cancelar = () => conversacion.CancelarPropuesta(respuesta.Id, Ahora.AddMinutes(2));
        otraVez.Should().Throw<DomainException>().WithMessage("*ya se envió (trámite T-000007)*");
        cancelar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Una_propuesta_cancelada_o_vencida_no_se_puede_confirmar()
    {
        var conversacion = Nueva();
        var (_, cancelada) = conversacion.Registrar("Quiero una constancia", "Te preparé la solicitud.", [], TipoRespuesta.Respuesta, Ahora, Propuesta());
        var (_, vieja) = conversacion.Registrar("Otra constancia", "Te preparé otra.", [], TipoRespuesta.Respuesta, Ahora, Propuesta());

        conversacion.CancelarPropuesta(cancelada.Id, Ahora).Propuesta!.Estado.Should().Be(EstadoPropuesta.Cancelada);

        var confirmarCancelada = () => conversacion.ConfirmarPropuesta(cancelada.Id, Guid.NewGuid(), "T-1", Ahora);
        var confirmarVencida = () => conversacion.ConfirmarPropuesta(vieja.Id, Guid.NewGuid(), "T-1", Ahora.AddHours(Conversacion.HorasDeVigenciaPropuesta).AddMinutes(1));
        confirmarCancelada.Should().Throw<DomainException>().WithMessage("*se canceló*");
        confirmarVencida.Should().Throw<DomainException>().WithMessage("*venció*");
    }

    [Fact]
    public void Un_mensaje_sin_propuesta_no_se_puede_confirmar()
    {
        var conversacion = Nueva();
        var (pregunta, respuesta) = conversacion.Registrar("¿Cuándo cierra?", "Cierra 48 horas antes [1].", [Fuente], TipoRespuesta.Respuesta, Ahora);

        var confirmarRespuesta = () => conversacion.ConfirmarPropuesta(respuesta.Id, Guid.NewGuid(), "T-1", Ahora);
        var confirmarPregunta = () => conversacion.ConfirmarPropuesta(pregunta.Id, Guid.NewGuid(), "T-1", Ahora);

        confirmarRespuesta.Should().Throw<DomainException>().WithMessage("*no tiene una solicitud*");
        confirmarPregunta.Should().Throw<DomainException>();
    }

    [Fact]
    public void Una_aclaracion_es_una_pregunta_corta_sin_fuentes_ni_propuesta()
    {
        var conversacion = Nueva();

        var (_, aclaracion) = conversacion.Registrar("Quiero una constancia", "¿De qué carrera sos?", [], TipoRespuesta.Aclaracion, Ahora);
        var conFuentes = () => conversacion.Registrar("x", "¿Cuál?", [Fuente], TipoRespuesta.Aclaracion, Ahora);
        var larga = () => conversacion.Registrar("x", new string('a', Conversacion.LargoMaximoAclaracion + 1) + "?", [], TipoRespuesta.Aclaracion, Ahora);
        var abstencionConPropuesta = () => conversacion.Registrar("x", "No sé.", [], TipoRespuesta.SinEvidencia, Ahora, Propuesta());

        aclaracion.EsAclaracion.Should().BeTrue();
        aclaracion.SinEvidencia.Should().BeFalse();
        conFuentes.Should().Throw<DomainException>();
        larga.Should().Throw<DomainException>();
        abstencionConPropuesta.Should().Throw<DomainException>();
    }

    [Fact]
    public void Una_respuesta_muy_larga_se_recorta()
    {
        var (_, respuesta) = Nueva().Registrar("¿?", new string('x', 5000), [Fuente], TipoRespuesta.Respuesta, Ahora);

        respuesta.Contenido.Should().HaveLength(Conversacion.LargoMaximoRespuesta).And.EndWith("…");
    }
}
