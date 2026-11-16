using System.Text;
using Astrea.Domain.Common;
using Astrea.Domain.Knowledge;
using AwesomeAssertions;

namespace Astrea.Domain.UnitTests.Knowledge;

public class DocumentoRagTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secretaria = Guid.NewGuid();
    private static readonly string Hash = new('a', 64);

    private static DatosDocumentoRag Datos(string titulo = "Reglamento Académico Marco") =>
        new(titulo, TipoDocumentoRag.Reglamento, "Resolución 4043/09", new DateOnly(2024, 3, 1));

    private static DocumentoRag Nuevo() => DocumentoRag.Cargar(Guid.NewGuid(), Datos(), "documentos-rag/2026/x.pdf", Hash, Secretaria, Ahora);

    private static DocumentoRag Indexado()
    {
        var documento = Nuevo();
        documento.IniciarProcesamiento();
        documento.MarcarIndexado(10, 25, Ahora);
        return documento;
    }

    [Fact]
    public void Un_documento_nuevo_queda_pendiente_en_la_version_1()
    {
        var documento = Nuevo();

        documento.Estado.Should().Be(EstadoIndexacion.Pendiente);
        documento.Version.Should().Be(1);
        documento.SerieId.Should().NotBeEmpty();
        documento.EsConsultable.Should().BeFalse();
    }

    [Fact]
    public void Recorre_pendiente_procesando_indexado()
    {
        var documento = Nuevo();

        documento.IniciarProcesamiento();
        documento.Estado.Should().Be(EstadoIndexacion.Procesando);
        documento.Intentos.Should().Be(1);

        documento.MarcarIndexado(10, 25, Ahora);
        documento.Estado.Should().Be(EstadoIndexacion.Indexado);
        documento.CantidadFragmentos.Should().Be(25);
        documento.EsConsultable.Should().BeTrue();
    }

    [Fact]
    public void Un_error_queda_registrado_y_se_puede_reintentar()
    {
        var documento = Nuevo();
        documento.IniciarProcesamiento();

        documento.MarcarError("El PDF está protegido con contraseña.");
        documento.Estado.Should().Be(EstadoIndexacion.Error);
        documento.ErrorProcesamiento.Should().Contain("contraseña");

        documento.Reindexar();
        documento.Estado.Should().Be(EstadoIndexacion.Pendiente);
        documento.ErrorProcesamiento.Should().BeNull();
        documento.IniciarProcesamiento();
        documento.Intentos.Should().Be(2);
    }

    [Fact]
    public void No_se_marca_indexado_sin_fragmentos_ni_sin_estar_procesando()
    {
        var documento = Nuevo();
        ((Action)(() => documento.MarcarIndexado(1, 5, Ahora))).Should().Throw<DomainException>();

        documento.IniciarProcesamiento();
        ((Action)(() => documento.MarcarIndexado(1, 0, Ahora))).Should().Throw<DomainException>().WithMessage("*al menos un fragmento*");
    }

    [Fact]
    public void Una_nueva_version_comparte_la_serie_y_sube_el_numero()
    {
        var anterior = Indexado();

        var nueva = anterior.NuevaVersion(Guid.NewGuid(), anterior.Version, Datos("Reglamento Académico Marco 2026"), "documentos-rag/2026/y.pdf", new string('b', 64), Secretaria, Ahora);

        nueva.SerieId.Should().Be(anterior.SerieId);
        nueva.Version.Should().Be(2);
        nueva.Estado.Should().Be(EstadoIndexacion.Pendiente);
        anterior.Estado.Should().Be(EstadoIndexacion.Indexado, "la anterior sigue vigente hasta que la nueva se indexe");
    }

    [Fact]
    public void Una_nueva_version_con_el_mismo_archivo_se_rechaza()
    {
        var anterior = Indexado();

        var repetida = () => anterior.NuevaVersion(Guid.NewGuid(), 1, Datos(), "documentos-rag/2026/y.pdf", Hash.ToUpperInvariant(), Secretaria, Ahora);

        repetida.Should().Throw<DomainException>().WithMessage("*idéntico*");
    }

    [Fact]
    public void Reemplazado_y_retirado_no_se_reindexan()
    {
        var reemplazado = Indexado();
        reemplazado.MarcarReemplazado();
        var retirado = Indexado();
        retirado.Retirar(Secretaria);

        ((Action)reemplazado.Reindexar).Should().Throw<DomainException>().WithMessage("*más nueva*");
        ((Action)retirado.Reindexar).Should().Throw<DomainException>().WithMessage("*retirado*");
        retirado.RetiradoPorUsuarioId.Should().Be(Secretaria);
        retirado.EsConsultable.Should().BeFalse();
    }

    [Fact]
    public void Si_se_corta_el_proceso_vuelve_a_la_cola()
    {
        var documento = Nuevo();
        documento.IniciarProcesamiento();

        documento.DevolverALaCola();

        documento.Estado.Should().Be(EstadoIndexacion.Pendiente);
        ((Action)(() => documento.Retirar(Secretaria))).Should().NotThrow();
    }

    [Theory]
    [InlineData("%PDF-1.7\n...", true)]
    [InlineData("<html>no</html>", false)]
    [InlineData("", false)]
    public void Valida_que_el_archivo_sea_un_pdf_por_su_contenido(string contenido, bool valido)
    {
        var validar = () => DocumentoRag.ValidarArchivo(Encoding.ASCII.GetBytes(contenido));

        if (valido)
        {
            validar.Should().NotThrow();
        }
        else
        {
            validar.Should().Throw<DomainException>();
        }
    }

    [Fact]
    public void Un_pdf_de_mas_de_20_MB_se_rechaza()
    {
        byte[] enorme = [.. "%PDF-1.7"u8, .. new byte[DocumentoRag.TamanioMaximoBytes]];

        ((Action)(() => DocumentoRag.ValidarArchivo(enorme))).Should().Throw<DomainException>().WithMessage("*20 MB*");
    }

    [Fact]
    public void El_fragmento_exige_un_embedding_de_768_dimensiones()
    {
        var metadata = new MetadataFragmento("Reglamento", TipoDocumentoRag.Reglamento, 1, null, null, null);

        var corto = () => new FragmentoRag(Guid.NewGuid(), Guid.NewGuid(), 1, "texto", new float[10], metadata, 1, 1);
        var paginas = () => new FragmentoRag(Guid.NewGuid(), Guid.NewGuid(), 1, "texto", new float[FragmentoRag.DimensionesEmbedding], metadata, 3, 2);

        corto.Should().Throw<DomainException>().WithMessage("*768*");
        paginas.Should().Throw<DomainException>().WithMessage("*páginas*");
    }
}
