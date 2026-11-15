using System.Buffers.Binary;
using Astrea.Domain.Common;

namespace Astrea.Domain.LostAndFound;

/// <summary>
/// Validación de la foto de un hallazgo: solo JPEG, PNG o WebP, con tamaño máximo, y comprobando el contenido real
/// (los primeros bytes), no lo que declara el navegador. También quita los metadatos EXIF, que en las fotos de celular
/// suelen incluir la ubicación GPS y el modelo del teléfono de quien la sacó.
/// </summary>
public static class FotoHallazgo
{
    public const int TamanioMaximoBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyList<string> TiposPermitidos = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Devuelve la foto sin metadatos, con su tipo real y la extensión para el nombre del archivo.</summary>
    public static (byte[] Contenido, string ContentType, string Extension) Validar(byte[] contenido, string? contentTypeDeclarado)
    {
        if (contenido.Length == 0)
        {
            throw new DomainException("La foto está vacía.");
        }

        if (contenido.Length > TamanioMaximoBytes)
        {
            throw new DomainException($"La foto no puede superar los {TamanioMaximoBytes / (1024 * 1024)} MB.");
        }

        var (tipoReal, extension) = Detectar(contenido)
            ?? throw new DomainException("La foto tiene que ser una imagen JPEG, PNG o WebP.");

        var declarado = contentTypeDeclarado?.Split(';')[0].Trim().ToLowerInvariant();
        if (declarado is not null && declarado != tipoReal && !(declarado == "image/jpg" && tipoReal == "image/jpeg"))
        {
            throw new DomainException("El tipo de archivo no coincide con su contenido.");
        }

        var limpio = tipoReal switch
        {
            "image/jpeg" => QuitarMetadatosJpeg(contenido),
            "image/png" => QuitarMetadatosPng(contenido),
            _ => QuitarMetadatosWebp(contenido),
        };

        return (limpio, tipoReal, extension);
    }

    private static (string, string)? Detectar(byte[] bytes) =>
        bytes switch
        {
            [0xFF, 0xD8, 0xFF, ..] => ("image/jpeg", "jpg"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => ("image/png", "png"),
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => ("image/webp", "webp"),
            _ => null,
        };

    /// <summary>Quita los segmentos APP1 (EXIF/XMP) y comentarios de un JPEG, conservando la imagen.</summary>
    private static byte[] QuitarMetadatosJpeg(byte[] jpeg)
    {
        using var salida = new MemoryStream(jpeg.Length);
        salida.Write(jpeg, 0, 2); // SOI
        var posicion = 2;
        while (posicion + 4 <= jpeg.Length && jpeg[posicion] == 0xFF)
        {
            var marcador = jpeg[posicion + 1];
            if (marcador == 0xDA)
            {
                // Inicio de los datos de imagen: se copia el resto tal cual.
                salida.Write(jpeg, posicion, jpeg.Length - posicion);
                return salida.ToArray();
            }

            var largo = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(posicion + 2, 2));
            var fin = posicion + 2 + largo;
            if (largo < 2 || fin > jpeg.Length)
            {
                throw new DomainException("La foto JPEG está dañada.");
            }

            var esMetadato = marcador is 0xE1 or 0xFE; // APP1 (EXIF, XMP) y comentarios
            if (!esMetadato)
            {
                salida.Write(jpeg, posicion, fin - posicion);
            }

            posicion = fin;
        }

        throw new DomainException("La foto JPEG está dañada.");
    }

    /// <summary>Quita los bloques de metadatos de un PNG (eXIf, textos y fecha), conservando la imagen.</summary>
    private static byte[] QuitarMetadatosPng(byte[] png)
    {
        string[] metadatos = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];
        using var salida = new MemoryStream(png.Length);
        salida.Write(png, 0, 8);
        var posicion = 8;
        while (posicion + 12 <= png.Length)
        {
            var largo = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(posicion, 4));
            var tipo = System.Text.Encoding.ASCII.GetString(png, posicion + 4, 4);
            var fin = posicion + 12 + largo;
            if (largo < 0 || fin > png.Length)
            {
                throw new DomainException("La foto PNG está dañada.");
            }

            if (!metadatos.Contains(tipo))
            {
                salida.Write(png, posicion, fin - posicion);
            }

            posicion = fin;
            if (tipo == "IEND")
            {
                return salida.ToArray();
            }
        }

        throw new DomainException("La foto PNG está dañada.");
    }

    /// <summary>
    /// Quita los bloques EXIF y XMP de un WebP (contenedor RIFF), apaga sus indicadores en el bloque VP8X
    /// y recalcula el tamaño del contenedor.
    /// </summary>
    private static byte[] QuitarMetadatosWebp(byte[] webp)
    {
        using var bloques = new MemoryStream(webp.Length);
        var posicion = 12;
        while (posicion + 8 <= webp.Length)
        {
            var tipo = System.Text.Encoding.ASCII.GetString(webp, posicion, 4);
            var largo = (int)BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(posicion + 4, 4));
            var fin = posicion + 8 + largo + (largo % 2); // los bloques se alinean a 2 bytes
            if (largo < 0 || fin > webp.Length)
            {
                throw new DomainException("La foto WebP está dañada.");
            }

            if (tipo is not ("EXIF" or "XMP "))
            {
                var bloque = webp.AsSpan(posicion, fin - posicion).ToArray();
                if (tipo == "VP8X" && bloque.Length > 8)
                {
                    bloque[8] &= unchecked((byte)~0x0C); // indicadores de EXIF (0x08) y XMP (0x04)
                }

                bloques.Write(bloque);
            }

            posicion = fin;
        }

        var cuerpo = bloques.ToArray();
        var salida = new byte[12 + cuerpo.Length];
        webp.AsSpan(0, 12).CopyTo(salida);
        BinaryPrimitives.WriteUInt32LittleEndian(salida.AsSpan(4, 4), (uint)(4 + cuerpo.Length));
        cuerpo.CopyTo(salida, 12);
        return salida;
    }
}
