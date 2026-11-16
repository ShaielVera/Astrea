using Astrea.Application.Abstractions.Documents;
using Astrea.Domain.Knowledge;
using Astrea.Infrastructure.Documents;
using AwesomeAssertions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Astrea.Infrastructure.IntegrationTests.Documents;

/// <summary>Extracción de texto de un PDF real (generado con QuestPDF), de punta a punta hasta los fragmentos.</summary>
public sealed class PdfPigExtractorTests
{
    static PdfPigExtractorTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static byte[] Pdf(params string[][] paginas) =>
        Document.Create(documento =>
        {
            foreach (var lineas in paginas)
            {
                documento.Page(pagina =>
                {
                    pagina.Margin(40);
                    pagina.Content().Column(columna =>
                    {
                        foreach (var linea in lineas)
                        {
                            columna.Item().Text(linea);
                        }
                    });
                });
            }
        }).GeneratePdf();

    [Fact]
    public void Extrae_el_texto_de_cada_pagina_con_tildes()
    {
        var paginas = new PdfPigExtractor().Extraer(Pdf(
            ["CAPÍTULO I", "DE LA INSCRIPCIÓN", "La inscripción a mesas cierra 48 horas antes."],
            ["CAPÍTULO II", "Los estudiantes regulares conservan la regularidad dos años."]));

        paginas.Should().HaveCount(2);
        paginas[0].Numero.Should().Be(1);
        paginas[0].Texto.Should().Contain("CAPÍTULO I").And.Contain("inscripción a mesas cierra 48 horas");
        paginas[1].Texto.Should().Contain("regularidad dos años");
    }

    [Fact]
    public void El_texto_extraido_se_fragmenta_por_capitulos_con_su_pagina()
    {
        var paginas = new PdfPigExtractor().Extraer(Pdf(
            ["CAPÍTULO I", "La inscripción a mesas cierra 48 horas antes."],
            ["CAPÍTULO II", "Los estudiantes regulares conservan la regularidad dos años."]));

        var fragmentos = Fragmentador.Fragmentar(paginas);

        fragmentos.Should().HaveCount(2);
        fragmentos[1].Should().BeEquivalentTo(new { Seccion = "CAPÍTULO II", PaginaDesde = 2, PaginaHasta = 2 });
    }

    [Fact]
    public void Un_archivo_dañado_da_un_error_claro()
    {
        var extraer = () => new PdfPigExtractor().Extraer("%PDF-1.7 esto no es un PDF de verdad"u8.ToArray());

        extraer.Should().Throw<PdfIlegibleException>().WithMessage("*dañado*");
    }
}
