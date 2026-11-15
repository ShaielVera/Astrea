using Astrea.Application.Abstractions.Documents;
using Astrea.Infrastructure.Documents;
using AwesomeAssertions;

namespace Astrea.Infrastructure.IntegrationTests.Documents;

public sealed class CarpetaPrivadaStorageTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"astrea-storage-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }

    [Fact]
    public async Task Guarda_lee_y_elimina_un_archivo_dentro_de_la_carpeta()
    {
        var storage = new CarpetaPrivadaStorage(_raiz);
        byte[] contenido = [1, 2, 3];

        await storage.GuardarAsync("documentos/2026/a.pdf", contenido, CancellationToken.None);
        var leido = await storage.LeerAsync("documentos/2026/a.pdf", CancellationToken.None);
        await storage.EliminarAsync("documentos/2026/a.pdf", CancellationToken.None);

        leido.Should().Equal(contenido);
        File.Exists(Path.Combine(_raiz, "documentos", "2026", "a.pdf")).Should().BeFalse();
        (await storage.LeerAsync("documentos/2026/a.pdf", CancellationToken.None)).Should().BeNull();
        Directory.GetFiles(_raiz, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Theory]
    [InlineData("../fuera.pdf")]
    [InlineData("documentos/../../fuera.pdf")]
    public async Task No_permite_salir_de_la_carpeta_privada(string ruta)
    {
        IFileStorage storage = new CarpetaPrivadaStorage(_raiz);

        var guardar = () => storage.GuardarAsync(ruta, [1], CancellationToken.None);

        await guardar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*sale de la carpeta privada*");
    }

    [Fact]
    public void Genera_un_pdf_valido()
    {
        var pdf = new QuestPdfGenerator().Generar(new ContenidoDocumento(
            "Instituto de Prueba",
            "Constancia",
            "Primer párrafo.\n\nSegundo párrafo.",
            "T-000001",
            "ABCD-EFGH-JKMN",
            "https://astrea.test/validar/ABCD-EFGH-JKMN",
            DateTimeOffset.UtcNow,
            1));

        pdf.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }
}
