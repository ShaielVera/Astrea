using Astrea.Domain.Agent;
using Astrea.Domain.Common;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Agent;

public class ConsultaRegistradaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static ConsultaRegistrada Registrar(
        string pregunta = "¿Cuándo cierra la inscripción?",
        ResultadoConsulta resultado = ResultadoConsulta.Respondida,
        MotivoConsulta motivo = MotivoConsulta.Ninguno,
        DateTimeOffset? fecha = null) =>
        ConsultaRegistrada.Registrar(Guid.NewGuid(), pregunta, resultado, motivo, fecha ?? Ahora, TimeSpan.FromMilliseconds(1234.6), ["buscar_mesas", "buscar_mesas", "buscar_correlatividades"], 2, 1);

    [Theory]
    [InlineData("¿Cuándo cierra la inscripción?", "cuando cierra la inscripcion")]
    [InlineData("  cuando   CIERRA la inscripcion!!  ", "cuando cierra la inscripcion")]
    [InlineData("¿Qué correlativas tiene Programación II?", "que correlativas tiene programacion ii")]
    [InlineData("¿¡?", "")]
    public void Normaliza_la_pregunta_para_agrupar_temas_iguales(string pregunta, string tema) =>
        ConsultaRegistrada.Normalizar(pregunta).Should().Be(tema);

    [Fact]
    public void El_tema_nunca_guarda_datos_personales()
    {
        var tema = ConsultaRegistrada.Normalizar("Soy 30.123.456, mi mail es ana.perez@gmail.com y mi cel 11 4567-8901");

        tema.Should().Be("soy [dni] mi mail es [email] y mi cel [telefono]");
        tema.Should().NotContain("30").And.NotContain("perez").And.NotContain("4567");
    }

    [Fact]
    public void Recorta_el_tema_y_guarda_una_pregunta_vacia_como_tal()
    {
        Registrar(new string('a', 500)).Tema.Should().HaveLength(ConsultaRegistrada.LargoMaximoTema);
        Registrar("¿?").Tema.Should().Be("(vacía)");
    }

    [Fact]
    public void Guarda_el_dia_del_instituto_las_herramientas_sin_repetir_y_la_duracion()
    {
        // 01:30 UTC del 16 todavía es el 15 en Argentina.
        var consulta = Registrar(fecha: new DateTimeOffset(2026, 11, 16, 1, 30, 0, TimeSpan.Zero));

        consulta.Dia.Should().Be(new DateOnly(2026, 11, 15));
        consulta.Herramientas.Should().Equal("buscar_correlatividades", "buscar_mesas");
        consulta.DuracionMs.Should().Be(1234);
        consulta.FuentesDocumentos.Should().Be(2);
        consulta.FuentesDatos.Should().Be(1);
    }

    [Theory]
    [InlineData(ResultadoConsulta.Respondida, MotivoConsulta.CitasInvalidas)]
    [InlineData(ResultadoConsulta.Aclaracion, MotivoConsulta.SinInformacion)]
    [InlineData(ResultadoConsulta.SinEvidencia, MotivoConsulta.Ninguno)]
    [InlineData(ResultadoConsulta.Error, MotivoConsulta.Ninguno)]
    [InlineData((ResultadoConsulta)99, MotivoConsulta.Ninguno)]
    public void El_motivo_tiene_que_corresponder_al_resultado(ResultadoConsulta resultado, MotivoConsulta motivo) =>
        FluentActions.Invoking(() => Registrar(resultado: resultado, motivo: motivo)).Should().Throw<DomainException>();

    [Fact]
    public void No_acepta_cantidades_de_fuentes_negativas() =>
        FluentActions.Invoking(() => ConsultaRegistrada.Registrar(Guid.NewGuid(), "x", ResultadoConsulta.Respondida, MotivoConsulta.Ninguno, Ahora, TimeSpan.Zero, fuentesDocumentos: -1))
            .Should().Throw<DomainException>();
}
