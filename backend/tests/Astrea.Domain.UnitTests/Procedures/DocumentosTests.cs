using Astrea.Domain.Common;
using Astrea.Domain.Procedures;
using Astrea.Domain.Users;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Procedures;

public class DocumentosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static TramiteTipo Tipo(Guid id) => CatalogoTramites.Tipos().Single(tipo => tipo.Id == id);

    private static Tramite TramiteAlumnoRegular() =>
        Tramite.Solicitar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Tipo(CatalogoTramites.AlumnoRegularId),
            new Dictionary<string, string?> { ["carrera"] = Guid.NewGuid().ToString(), ["presentarAnte"] = "ANSES" },
            null,
            Ahora);

    private static PlantillaDocumento PlantillaAlumnoRegular() =>
        CatalogoTramites.PlantillasIniciales().Single(plantilla => plantilla.Id == CatalogoTramites.PlantillaAlumnoRegularV1Id);

    [Fact]
    public void La_plantilla_reemplaza_los_marcadores_y_usa_el_valor_por_defecto_si_falta_un_dato()
    {
        var plantilla = CatalogoTramites.PlantillasIniciales().Single(p => p.Id == CatalogoTramites.PlantillaConstanciaExamenV1Id);

        var texto = plantilla.Renderizar(new Dictionary<string, string>
        {
            ["nombreCompleto"] = "Ana Pérez",
            ["dni"] = "30.123.456",
            ["carrera"] = "Desarrollo de Software",
            ["materia"] = "Programación II",
            ["fechaExamen"] = "10/12/2026",
            ["fechaEmision"] = "15 de noviembre de 2026",
        });

        texto.Should().Contain("Ana Pérez, DNI 30.123.456").And.Contain("Programación II el día 10/12/2026")
            .And.Contain("presentada ante quien corresponda").And.NotContain("{{");
    }

    [Fact]
    public void Una_plantilla_con_un_dato_inexistente_se_rechaza_y_dice_cuales_hay()
    {
        var crear = () => PlantillaDocumento.Crear(Guid.NewGuid(), Tipo(CatalogoTramites.AlumnoRegularId), 2, "Constancia", "Hola {{nombreCompleto}}, tu promedio es {{promedio}}.", Ahora);

        crear.Should().Throw<DomainException>().WithMessage("*{{promedio}}*Disponibles*{{presentarAnte}}*");
    }

    [Fact]
    public void Las_plantillas_iniciales_son_las_de_los_tramites_que_emiten_pdf()
    {
        CatalogoTramites.PlantillasIniciales().Select(plantilla => plantilla.TramiteTipoId)
            .Should().BeEquivalentTo([CatalogoTramites.AlumnoRegularId, CatalogoTramites.ConstanciaExamenId]);
    }

    [Fact]
    public void El_documento_emitido_guarda_hash_version_y_solo_los_ultimos_digitos_del_dni()
    {
        var tramite = TramiteAlumnoRegular();
        byte[] pdf = [0x25, 0x50, 0x44, 0x46];

        var documento = DocumentoEmitido.Emitir(Guid.NewGuid(), tramite, PlantillaAlumnoRegular(), "Constancia de alumno regular", "abcd-efgh-jkmn", "documentos/2026/x.pdf", pdf, "30123456", Ahora);

        documento.CodigoValidacion.Should().Be("ABCD-EFGH-JKMN");
        documento.HashSha256.Should().HaveLength(64).And.Be(DocumentoEmitido.CalcularHash(pdf));
        documento.DniUltimosDigitos.Should().Be("456");
        documento.VersionPlantilla.Should().Be(1);
        documento.UsuarioId.Should().Be(tramite.UsuarioId);
        documento.Estado.Should().Be(EstadoDocumento.Vigente);
    }

    [Fact]
    public void Una_plantilla_de_otro_tipo_no_sirve_para_emitir()
    {
        var plantillaExamen = CatalogoTramites.PlantillasIniciales().Single(p => p.Id == CatalogoTramites.PlantillaConstanciaExamenV1Id);

        var emitir = () => DocumentoEmitido.Emitir(Guid.NewGuid(), TramiteAlumnoRegular(), plantillaExamen, "x", DocumentoEmitido.GenerarCodigo(), "a.pdf", [1], null, Ahora);

        emitir.Should().Throw<DomainException>().WithMessage("*no corresponde*");
    }

    [Fact]
    public void Los_codigos_generados_tienen_formato_legible_y_no_se_repiten()
    {
        var codigos = Enumerable.Range(0, 500).Select(_ => DocumentoEmitido.GenerarCodigo()).ToList();

        codigos.Should().OnlyContain(codigo => System.Text.RegularExpressions.Regex.IsMatch(codigo, "^[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$"));
        codigos.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("abcd efgh jkmn", "ABCD-EFGH-JKMN")]
    [InlineData("ABCDEFGHJKMN", "ABCD-EFGH-JKMN")]
    [InlineData("1OI0-0000-0000", "1010-0000-0000")]
    [InlineData("ABC", null)]
    [InlineData("ABCD-EFGH-JKMU", null)]
    public void El_codigo_tipeado_se_normaliza(string tipeado, string? esperado)
    {
        DocumentoEmitido.NormalizarCodigo(tipeado).Should().Be(esperado);
    }

    [Fact]
    public void Anular_exige_motivo_y_no_se_anula_dos_veces()
    {
        var documento = DocumentoEmitido.Emitir(Guid.NewGuid(), TramiteAlumnoRegular(), PlantillaAlumnoRegular(), "x", DocumentoEmitido.GenerarCodigo(), "a.pdf", [1], null, Ahora);
        var admin = Guid.NewGuid();

        ((Action)(() => documento.Anular(admin, " ", Ahora))).Should().Throw<DomainException>().WithMessage("*motivo*");
        documento.Anular(admin, "Emitido con un error de tipeo", Ahora);

        documento.Estado.Should().Be(EstadoDocumento.Anulado);
        documento.AnuladoPorUsuarioId.Should().Be(admin);
        ((Action)(() => documento.Anular(admin, "otra vez", Ahora))).Should().Throw<DomainException>().WithMessage("*ya está anulado*");
    }

    [Fact]
    public void Aprobar_un_tramite_que_ya_esta_listo_no_hace_nada()
    {
        var tramite = TramiteAlumnoRegular();
        var admin = Guid.NewGuid();
        tramite.Aprobar(admin, "Primera", Ahora);

        tramite.Aprobar(admin, "Segunda", Ahora.AddMinutes(1));

        tramite.Eventos.Should().HaveCount(2);
        tramite.ObservacionAdministrativa.Should().Be("Primera");
    }

    [Theory]
    [InlineData("30.123.456", "30123456")]
    [InlineData("30 123 456", "30123456")]
    [InlineData("5123456", "5123456")]
    public void Los_datos_oficiales_guardan_el_dni_solo_con_digitos(string dni, string esperado)
    {
        var usuario = Usuario.Registrar(Guid.NewGuid(), "uid", "ana@astrea.test", Ahora);

        usuario.ActualizarDatosOficiales(" Ana ", " Pérez ", dni, null, Ahora);

        usuario.Dni.Should().Be(esperado);
        usuario.Nombre.Should().Be("Ana");
        usuario.TieneDatosOficiales.Should().BeTrue();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("30.123.45A")]
    [InlineData("1234567890")]
    public void Un_dni_invalido_se_rechaza(string dni)
    {
        var usuario = Usuario.Registrar(Guid.NewGuid(), "uid", "ana@astrea.test", Ahora);

        var actualizar = () => usuario.ActualizarDatosOficiales("Ana", "Pérez", dni, null, Ahora);

        actualizar.Should().Throw<DomainException>().WithMessage("*DNI*");
        usuario.TieneDatosOficiales.Should().BeFalse();
    }
}
