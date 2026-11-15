using System.Buffers.Binary;
using System.Text;
using Astrea.Domain.Common;
using Astrea.Domain.LostAndFound;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.LostAndFound;

public class ObjetoPerdidoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 15);
    private static readonly Guid Estudiante = Guid.NewGuid();
    private static readonly Guid Secretaria = Guid.NewGuid();

    private static DatosHallazgo Datos(DateOnly? fecha = null, PuntoResguardo punto = PuntoResguardo.Secretaria) =>
        new("Termo negro", "Tiene una calcomanía de Racing y una abolladura en la base", CategoriaObjeto.TermoOBotella, "Aula 3, planta baja", FranjaHoraria.Manana, punto, fecha ?? Hoy);

    private static ObjetoPerdido Publicado() => ObjetoPerdido.Publicar(Guid.NewGuid(), Estudiante, Datos(), Hoy, Ahora);

    [Fact]
    public void Un_hallazgo_nuevo_queda_publicado_con_sus_datos()
    {
        var objeto = Publicado();

        objeto.Estado.Should().Be(EstadoObjeto.Publicado);
        objeto.PublicadoPorUsuarioId.Should().Be(Estudiante);
        objeto.PuntoResguardo.Should().Be(PuntoResguardo.Secretaria);
        objeto.FechaCierre.Should().BeNull();
    }

    [Fact]
    public void Los_puntos_de_resguardo_son_los_del_instituto_con_la_direccion_primero()
    {
        Enum.GetValues<PuntoResguardo>().Should().Equal(
            PuntoResguardo.Direccion, PuntoResguardo.Secretaria, PuntoResguardo.Biblioteca, PuntoResguardo.AulaInfraestructura, PuntoResguardo.Deposito);
    }

    [Fact]
    public void No_se_publica_con_fecha_futura_ni_de_hace_mas_de_60_dias()
    {
        ((Action)(() => ObjetoPerdido.Publicar(Guid.NewGuid(), Estudiante, Datos(Hoy.AddDays(1)), Hoy, Ahora))).Should().Throw<DomainException>().WithMessage("*futura*");
        ((Action)(() => ObjetoPerdido.Publicar(Guid.NewGuid(), Estudiante, Datos(Hoy.AddDays(-61)), Hoy, Ahora))).Should().Throw<DomainException>().WithMessage("*60 días*");
        ObjetoPerdido.Publicar(Guid.NewGuid(), Estudiante, Datos(Hoy.AddDays(-60)), Hoy, Ahora).Estado.Should().Be(EstadoObjeto.Publicado);
    }

    [Fact]
    public void Valores_fuera_de_las_listas_se_rechazan()
    {
        var publicar = () => ObjetoPerdido.Publicar(Guid.NewGuid(), Estudiante, Datos(punto: (PuntoResguardo)99), Hoy, Ahora);

        publicar.Should().Throw<DomainException>().WithMessage("*punto de resguardo*");
    }

    [Fact]
    public void Solo_aparece_en_la_busqueda_cuando_tiene_foto_y_sigue_publicado()
    {
        var objeto = Publicado();
        objeto.EsVisibleEnBusqueda.Should().BeFalse();

        objeto.AsignarFoto("hallazgos/2026/foto.png", "image/png");
        objeto.EsVisibleEnBusqueda.Should().BeTrue();

        objeto.DarDeBaja(Secretaria, "Duplicado", Ahora);
        objeto.EsVisibleEnBusqueda.Should().BeFalse();
    }

    [Fact]
    public void Se_depura_a_los_30_dias_de_cerrado_y_nunca_mientras_sigue_publicado()
    {
        var objeto = Publicado();
        objeto.DebeDepurarse(Ahora.AddYears(1)).Should().BeFalse();

        objeto.Entregar(Guid.NewGuid(), Secretaria, "Juan", "30123456", true, null, Ahora);

        objeto.DebeDepurarse(Ahora.AddDays(29)).Should().BeFalse();
        objeto.DebeDepurarse(Ahora.AddDays(ObjetoPerdido.DiasDeConservacion)).Should().BeTrue();
    }

    [Fact]
    public void Solo_quien_lo_publico_o_la_secretaria_pueden_editarlo()
    {
        var objeto = Publicado();

        objeto.PuedeEditar(Estudiante, esAdministrador: false).Should().BeTrue();
        objeto.PuedeEditar(Guid.NewGuid(), esAdministrador: true).Should().BeTrue();
        objeto.PuedeEditar(Guid.NewGuid(), esAdministrador: false).Should().BeFalse();
    }

    [Fact]
    public void Entregar_exige_la_verificacion_presencial_explicita()
    {
        var objeto = Publicado();

        var sinVerificar = () => objeto.Entregar(Guid.NewGuid(), Secretaria, "Juan López", "30123456", identidadVerificada: false, null, Ahora);

        sinVerificar.Should().Throw<DomainException>().WithMessage("*verificaste en persona*");
        objeto.Estado.Should().Be(EstadoObjeto.Publicado);
    }

    [Fact]
    public void La_entrega_registra_quien_retiro_quien_verifico_y_cuando()
    {
        var objeto = Publicado();

        var entrega = objeto.Entregar(Guid.NewGuid(), Secretaria, " Juan López ", "30.123.456", identidadVerificada: true, "Describió la calcomanía", Ahora);

        objeto.Estado.Should().Be(EstadoObjeto.Entregado);
        objeto.FechaCierre.Should().Be(Ahora);
        entrega.NombreRetirante.Should().Be("Juan López");
        entrega.DniRetirante.Should().Be("30123456");
        entrega.VerificadoPorUsuarioId.Should().Be(Secretaria);
        entrega.IdentidadVerificada.Should().BeTrue();
        entrega.FechaEntrega.Should().Be(Ahora);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("30I23456")]
    public void Un_dni_invalido_de_quien_retira_se_rechaza(string dni)
    {
        var entregar = () => Publicado().Entregar(Guid.NewGuid(), Secretaria, "Juan", dni, identidadVerificada: true, null, Ahora);

        entregar.Should().Throw<DomainException>().WithMessage("*DNI*");
    }

    [Fact]
    public void Un_objeto_entregado_no_se_vuelve_a_entregar_ni_se_da_de_baja_ni_cambia_la_foto()
    {
        var objeto = Publicado();
        objeto.Entregar(Guid.NewGuid(), Secretaria, "Juan", "30123456", true, null, Ahora);

        ((Action)(() => objeto.Entregar(Guid.NewGuid(), Secretaria, "Otro", "30123457", true, null, Ahora))).Should().Throw<DomainException>().WithMessage("*ya fue entregado*");
        ((Action)(() => objeto.DarDeBaja(Secretaria, "x", Ahora))).Should().Throw<DomainException>().WithMessage("*ya fue entregado*");
        ((Action)(() => objeto.AsignarFoto("hallazgos/x.png", "image/png"))).Should().Throw<DomainException>();
    }

    [Fact]
    public void La_baja_exige_motivo_y_registra_quien_la_hizo()
    {
        var objeto = Publicado();

        ((Action)(() => objeto.DarDeBaja(Secretaria, " ", Ahora))).Should().Throw<DomainException>().WithMessage("*motivo*");
        objeto.DarDeBaja(Secretaria, "Publicación duplicada", Ahora);

        objeto.Estado.Should().Be(EstadoObjeto.DadoDeBaja);
        objeto.DadoDeBajaPorUsuarioId.Should().Be(Secretaria);
        objeto.MotivoBaja.Should().Be("Publicación duplicada");
    }

    [Fact]
    public void Reemplazar_la_foto_devuelve_la_anterior_para_borrarla()
    {
        var objeto = Publicado();

        objeto.AsignarFoto("hallazgos/a.png", "image/png").Should().BeNull();
        objeto.AsignarFoto("hallazgos/b.jpg", "image/jpeg").Should().Be("hallazgos/a.png");
        objeto.FotoContentType.Should().Be("image/jpeg");
    }
}

public class FotoHallazgoTests
{
    /// <summary>PNG de 1x1 píxel.</summary>
    internal static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static byte[] Segmento(byte marcador, string datos)
    {
        var contenido = Encoding.ASCII.GetBytes(datos);
        var segmento = new byte[4 + contenido.Length];
        segmento[0] = 0xFF;
        segmento[1] = marcador;
        BinaryPrimitives.WriteUInt16BigEndian(segmento.AsSpan(2), (ushort)(contenido.Length + 2));
        contenido.CopyTo(segmento, 4);
        return segmento;
    }

    /// <summary>JPEG mínimo con un bloque EXIF que "contiene" coordenadas GPS.</summary>
    private static byte[] JpegConExif() =>
        [0xFF, 0xD8, .. Segmento(0xE0, "JFIF\0"), .. Segmento(0xE1, "Exif\0\0GPS-34.6,-58.4"), .. Segmento(0xDA, "\u0001\u0002"), 0x11, 0x22, 0xFF, 0xD9];

    private static byte[] Bloque(string tipo, byte[] datos)
    {
        var relleno = datos.Length % 2;
        var bloque = new byte[8 + datos.Length + relleno];
        Encoding.ASCII.GetBytes(tipo).CopyTo(bloque, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bloque.AsSpan(4), (uint)datos.Length);
        datos.CopyTo(bloque, 8);
        return bloque;
    }

    private static byte[] WebpConExif()
    {
        byte[] cuerpo = [.. Encoding.ASCII.GetBytes("WEBP"), .. Bloque("VP8X", [0x08, 0, 0, 0, 0, 0, 0, 0, 0, 0]), .. Bloque("EXIF", Encoding.ASCII.GetBytes("GPS!")), .. Bloque("VP8 ", [1, 2])];
        var riff = new byte[8 + cuerpo.Length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(riff, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(riff.AsSpan(4), (uint)cuerpo.Length);
        cuerpo.CopyTo(riff, 8);
        return riff;
    }

    [Fact]
    public void Un_png_valido_se_acepta()
    {
        var (contenido, tipo, extension) = FotoHallazgo.Validar(Png, "image/png");

        tipo.Should().Be("image/png");
        extension.Should().Be("png");
        contenido.Should().Equal(Png);
    }

    [Fact]
    public void Al_png_se_le_quitan_los_metadatos()
    {
        var iend = Png.Length - 12;
        byte[] exif = [0, 0, 0, 4, .. Encoding.ASCII.GetBytes("eXIfGPS!"), 0, 0, 0, 0];
        byte[] conExif = [.. Png[..iend], .. exif, .. Png[iend..]];

        var (contenido, _, _) = FotoHallazgo.Validar(conExif, "image/png");

        contenido.Should().Equal(Png);
    }

    [Fact]
    public void Al_jpeg_se_le_quita_el_exif_y_se_conserva_la_imagen()
    {
        var (contenido, tipo, extension) = FotoHallazgo.Validar(JpegConExif(), "image/jpeg");

        tipo.Should().Be("image/jpeg");
        extension.Should().Be("jpg");
        Encoding.ASCII.GetString(contenido).Should().NotContain("GPS").And.Contain("JFIF");
        contenido[^4..].Should().Equal(0x11, 0x22, 0xFF, 0xD9);
    }

    [Fact]
    public void Al_webp_se_le_quita_el_exif_y_se_recalcula_el_contenedor()
    {
        var (contenido, tipo, _) = FotoHallazgo.Validar(WebpConExif(), "image/webp");

        tipo.Should().Be("image/webp");
        Encoding.ASCII.GetString(contenido).Should().NotContain("EXIF").And.NotContain("GPS");
        BinaryPrimitives.ReadUInt32LittleEndian(contenido.AsSpan(4)).Should().Be((uint)(contenido.Length - 8));
        contenido[20].Should().Be(0, "el indicador de EXIF del bloque VP8X se apaga");
    }

    [Fact]
    public void Un_archivo_que_no_es_imagen_se_rechaza_aunque_diga_ser_png()
    {
        var validar = () => FotoHallazgo.Validar(Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "image/png");

        validar.Should().Throw<DomainException>().WithMessage("*JPEG, PNG o WebP*");
    }

    [Fact]
    public void Un_tipo_declarado_distinto_del_contenido_se_rechaza()
    {
        var validar = () => FotoHallazgo.Validar(Png, "image/jpeg");

        validar.Should().Throw<DomainException>().WithMessage("*no coincide*");
    }

    [Fact]
    public void Una_foto_vacia_o_mayor_a_5_MB_se_rechaza()
    {
        ((Action)(() => FotoHallazgo.Validar([], "image/png"))).Should().Throw<DomainException>().WithMessage("*vacía*");
        ((Action)(() => FotoHallazgo.Validar(new byte[FotoHallazgo.TamanioMaximoBytes + 1], "image/png"))).Should().Throw<DomainException>().WithMessage("*5 MB*");
    }
}
