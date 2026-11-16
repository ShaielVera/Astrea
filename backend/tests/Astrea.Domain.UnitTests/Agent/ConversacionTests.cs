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

        var (pregunta, respuesta) = conversacion.Registrar("¿Cuándo cierra?", "Cierra 48 horas antes [1].", [Fuente], sinEvidencia: false, Ahora.AddMinutes(1));

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

        var sinFuentes = () => conversacion.Registrar("¿Cuándo cierra?", "Cierra 48 horas antes.", [], sinEvidencia: false, Ahora);
        var abstencionConFuentes = () => conversacion.Registrar("¿Cuándo cierra?", "No sé.", [Fuente], sinEvidencia: true, Ahora);

        sinFuentes.Should().Throw<DomainException>().WithMessage("*al menos una fuente*");
        abstencionConFuentes.Should().Throw<DomainException>();
        conversacion.Registrar("¿Y el fútbol?", "No encontré información.", [], sinEvidencia: true, Ahora).Respuesta.SinEvidencia.Should().BeTrue();
    }

    [Fact]
    public void Tiene_un_maximo_de_preguntas()
    {
        var conversacion = Nueva();
        for (var i = 0; i < Conversacion.PreguntasMaximas; i++)
        {
            conversacion.Registrar($"Pregunta {i}", "No encontré información.", [], sinEvidencia: true, Ahora.AddSeconds(i));
        }

        var otra = () => conversacion.Registrar("Una más", "No encontré información.", [], sinEvidencia: true, Ahora);

        otra.Should().Throw<DomainException>().WithMessage("*Empezá una nueva*");
    }

    [Fact]
    public void Los_ultimos_mensajes_salen_en_orden_cronologico()
    {
        var conversacion = Nueva();
        conversacion.Registrar("Primera", "R1", [Fuente], false, Ahora.AddMinutes(1));
        conversacion.Registrar("Segunda", "R2", [Fuente], false, Ahora.AddMinutes(2));

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

    [Fact]
    public void Una_respuesta_muy_larga_se_recorta()
    {
        var (_, respuesta) = Nueva().Registrar("¿?", new string('x', 5000), [Fuente], false, Ahora);

        respuesta.Contenido.Should().HaveLength(Conversacion.LargoMaximoRespuesta).And.EndWith("…");
    }
}
