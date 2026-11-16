using Astrea.Domain.Agent;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Agent;

public class DatosSensiblesTests
{
    [Theory]
    [InlineData("Mi DNI es 35123456, ¿puedo rendir?", "Mi DNI es [DNI], ¿puedo rendir?")]
    [InlineData("DNI 35.123.456", "DNI [DNI]")]
    [InlineData("dni 4.123.456 y 1234567", "dni [DNI] y [DNI]")]
    [InlineData("CUIL 20-35123456-3", "CUIL [CUIL]")]
    [InlineData("cuil 20351234563", "cuil [CUIL]")]
    [InlineData("escribime a juan.perez+isp@gmail.com", "escribime a [email]")]
    [InlineData("mi celular es 11 5555-1234", "mi celular es [teléfono]")]
    [InlineData("llamá al +54 9 11 4567-8901", "llamá al [teléfono]")]
    [InlineData("tel 011 4567-8901", "tel [teléfono]")]
    public void Oculta_datos_personales(string texto, string esperado) =>
        DatosSensibles.Ocultar(texto).Should().Be(esperado);

    [Theory]
    [InlineData("¿Qué dice la Resolución 4043/09 sobre las mesas de 2026?")]
    [InlineData("¿Se necesita el 75% de asistencia para regularizar?")]
    [InlineData("¿El turno de diciembre empieza el 12/12?")]
    [InlineData("¿Cuántas materias tiene 1° año del plan 2024?")]
    public void No_toca_preguntas_sin_datos_personales(string texto) =>
        DatosSensibles.Ocultar(texto).Should().Be(texto);
}
