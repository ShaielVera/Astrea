using Astrea.Domain.Common;
using Astrea.Domain.Users;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Users;

public class PerfilTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private static Usuario NuevoUsuario() => Usuario.Registrar(Guid.NewGuid(), "uid-1", "alumna@astrea.test", Ahora);

    [Fact]
    public void El_estudiante_completa_y_corrige_sus_datos_cuando_quiere()
    {
        var usuario = NuevoUsuario();
        var tsds = Guid.NewGuid();
        var pei = Guid.NewGuid();

        usuario.CompletarDatosPersonales(" Brenda ", "Vera", "40.123.456", "San Justo", [tsds], Ahora);
        usuario.ActualizarDatosOficiales("Brenda", "Vera", "40123456", "L-12", Ahora);
        usuario.CompletarDatosPersonales("Brenda Shaiel", "Vera", "40123456", "san  justo", [tsds, pei, tsds], Ahora);

        usuario.Nombre.Should().Be("Brenda Shaiel");
        usuario.Dni.Should().Be("40123456");
        usuario.Localidad.Should().Be("San Justo", "se guarda como figura en la lista");
        usuario.CarrerasIds.Should().Equal(tsds, pei);
    }

    [Theory]
    [InlineData("sAN jUSTO", "San Justo")]
    [InlineData("nare", "Naré")]
    [InlineData("Colonia Esther", "Esther")]
    [InlineData("caastacito", "Cayastacito")]
    [InlineData("  vera  y   pintado ", "Vera y Pintado")]
    public void La_localidad_se_reconoce_sin_importar_mayusculas_ni_tildes_y_por_sus_otros_nombres(string escrita, string guardada)
    {
        Localidades.Buscar(escrita)!.Nombre.Should().Be(guardada);
    }

    [Fact]
    public void Una_localidad_que_no_esta_en_la_lista_se_rechaza()
    {
        var completar = () => NuevoUsuario().CompletarDatosPersonales("Brenda", "Vera", "40123456", "Sanjusto", [], Ahora);

        completar.Should().Throw<DomainException>().WithMessage("*de la lista*");
        Localidades.Todas.Select(l => l.Nombre).Should().OnlyHaveUniqueItems();
        Localidades.Todas.SelectMany(l => l.OtrosNombres.Prepend(l.Nombre)).Select(n => Localidades.Buscar(n)).Should().NotContainNulls();
    }

    [Theory]
    [InlineData("342 412-3456", "3424123456")]
    [InlineData("0342 15 412-3456", "3424123456")]
    [InlineData("+54 9 342 412 3456", "3424123456")]
    [InlineData("11 15 2345-6789", "1123456789")]
    [InlineData("+54 11 2345 6789", "1123456789")]
    public void El_celular_se_guarda_con_codigo_de_area_y_sin_el_15(string escrito, string guardado)
    {
        Usuario.NormalizarCelular(escrito).Should().Be(guardado);
    }

    [Theory]
    [InlineData("4123456")]
    [InlineData("342 412 34567 89")]
    public void Un_celular_sin_codigo_de_area_se_rechaza(string escrito)
    {
        var normalizar = () => Usuario.NormalizarCelular(escrito);

        normalizar.Should().Throw<DomainException>().WithMessage("*código de área*");
    }

    [Fact]
    public void Cambiar_un_dato_de_contacto_pide_validarlo_de_nuevo_y_la_validacion_vence_al_anio()
    {
        var usuario = NuevoUsuario();
        usuario.CambiarEmailContacto("Brenda@Gmail.com ", Ahora);
        usuario.MarcarContactoVerificado(CanalContacto.Email, "brenda@gmail.com", Ahora);

        usuario.ContactoVigente(CanalContacto.Email, Ahora.AddDays(364)).Should().BeTrue();
        usuario.ContactoVigente(CanalContacto.Email, Ahora.AddDays(366)).Should().BeFalse("se revalida cada año");

        usuario.CambiarEmailContacto("brenda@gmail.com", Ahora.AddDays(1));
        usuario.EmailContactoVerificadoEn.Should().Be(Ahora, "el mismo email no pierde la validación");
        usuario.CambiarEmailContacto("otra@gmail.com", Ahora.AddDays(1));
        usuario.EmailContactoVerificadoEn.Should().BeNull();
        usuario.Invoking(u => u.MarcarContactoVerificado(CanalContacto.Email, "brenda@gmail.com", Ahora))
            .Should().Throw<DomainException>().WithMessage("*cambió*");
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@b")]
    [InlineData("con espacio@gmail.com")]
    public void Un_email_de_contacto_invalido_se_rechaza(string email)
    {
        var cambiar = () => NuevoUsuario().CambiarEmailContacto(email, Ahora);

        cambiar.Should().Throw<DomainException>().WithMessage("*email de contacto*");
    }

    [Fact]
    public void El_codigo_correcto_valida_una_sola_vez()
    {
        var (codigo, enClaro) = CodigoVerificacion.Generar(Guid.NewGuid(), CanalContacto.Email, "brenda@gmail.com", Ahora);

        enClaro.Should().MatchRegex("^[0-9]{6}$");
        codigo.Hash.Should().NotContain(enClaro);
        codigo.Validar($"{enClaro[..3]} {enClaro[3..]}", Ahora.AddMinutes(5)).Should().Be(ResultadoCodigo.Correcto);
        codigo.Validar(enClaro, Ahora.AddMinutes(6)).Should().Be(ResultadoCodigo.SinIntentos, "ya se usó");
    }

    [Fact]
    public void El_codigo_vence_a_los_10_minutos_y_admite_5_intentos()
    {
        var (vencido, enClaroVencido) = CodigoVerificacion.Generar(Guid.NewGuid(), CanalContacto.Celular, "3424123456", Ahora);
        var (codigo, enClaro) = CodigoVerificacion.Generar(Guid.NewGuid(), CanalContacto.Celular, "3424123456", Ahora);
        var incorrecto = enClaro == "123456" ? "654321" : "123456";

        vencido.Validar(enClaroVencido, Ahora.AddMinutes(10)).Should().Be(ResultadoCodigo.Vencido);
        for (var intento = 1; intento < CodigoVerificacion.IntentosMaximos; intento++)
        {
            codigo.Validar(incorrecto, Ahora).Should().Be(ResultadoCodigo.Incorrecto);
        }

        codigo.Validar(incorrecto, Ahora).Should().Be(ResultadoCodigo.SinIntentos);
        codigo.Validar(enClaro, Ahora).Should().Be(ResultadoCodigo.SinIntentos, "después de 5 errores ni el correcto sirve");
    }
}
