using System.Globalization;
using Astrea.Application.Abstractions.Documents;
using Astrea.Domain.Exams;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Astrea.Infrastructure.Documents;

/// <summary>
/// Genera los documentos oficiales con QuestPDF (licencia Community: gratuita para proyectos educativos y
/// organizaciones chicas). El pie lleva el QR que apunta a la validación pública.
/// </summary>
internal sealed class QuestPdfGenerator : IPdfGenerator
{
    static QuestPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generar(ContenidoDocumento contenido)
    {
        var qr = PngByteQRCodeHelper.GetQRCode(contenido.UrlValidacion, QRCodeGenerator.ECCLevel.M, 10);
        var emision = TimeZoneInfo.ConvertTime(contenido.FechaEmision, ZonaHorariaInstitucional.Zona)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(2.5f, Unit.Centimetre);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(12).LineHeight(1.5f));

            pagina.Header().Column(encabezado =>
            {
                encabezado.Item().Text(contenido.Institucion).SemiBold().FontSize(14);
                encabezado.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            });

            pagina.Content().PaddingVertical(1.5f, Unit.Centimetre).Column(cuerpo =>
            {
                cuerpo.Spacing(12);
                cuerpo.Item().PaddingBottom(12).AlignCenter().Text(contenido.Titulo.ToUpper(CultureInfo.GetCultureInfo("es-AR"))).Bold().FontSize(16);
                foreach (var parrafo in contenido.Cuerpo.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    cuerpo.Item().Text(parrafo).Justify();
                }
            });

            pagina.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(8).Row(pie =>
            {
                pie.ConstantItem(2.8f, Unit.Centimetre).Image(qr);
                pie.RelativeItem().PaddingLeft(10).AlignMiddle().Column(datos =>
                {
                    datos.Item().Text("Verificá la autenticidad de este documento escaneando el código QR o ingresando el código en:").FontSize(9);
                    datos.Item().Text(contenido.UrlValidacion).FontSize(9).FontColor(Colors.Blue.Darken2);
                    datos.Item().Text(texto =>
                    {
                        texto.DefaultTextStyle(estilo => estilo.FontSize(9));
                        texto.Span("Código de validación: ");
                        texto.Span(contenido.CodigoValidacion).Bold();
                    });
                    datos.Item().Text($"Trámite {contenido.NumeroTramite} · Emitido el {emision} · Plantilla v{contenido.VersionPlantilla}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        })).GeneratePdf();
    }
}
