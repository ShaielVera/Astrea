namespace Astrea.Application.Abstractions.Documents;

/// <summary>Lo que va impreso en un documento oficial. El cuerpo ya viene con los datos reemplazados.</summary>
public sealed record ContenidoDocumento(
    string Institucion,
    string Titulo,
    string Cuerpo,
    string NumeroTramite,
    string CodigoValidacion,
    string UrlValidacion,
    DateTimeOffset FechaEmision,
    int VersionPlantilla);

/// <summary>Puerto de salida: renderiza un documento oficial a PDF (con su QR de validación).</summary>
public interface IPdfGenerator
{
    byte[] Generar(ContenidoDocumento contenido);
}

/// <summary>Puerto de salida: almacenamiento privado de archivos. Nada de lo que se guarda es accesible por URL pública.</summary>
public interface IFileStorage
{
    Task GuardarAsync(string ruta, byte[] contenido, CancellationToken cancellationToken);

    /// <summary>Null si el archivo no existe.</summary>
    Task<byte[]?> LeerAsync(string ruta, CancellationToken cancellationToken);

    Task EliminarAsync(string ruta, CancellationToken cancellationToken);
}

/// <summary>Configuración de los documentos emitidos (sección "Documentos").</summary>
public sealed class ConfiguracionDocumentos
{
    public const string Seccion = "Documentos";

    /// <summary>Nombre que encabeza los documentos.</summary>
    public string Institucion { get; init; } = "Instituto Superior de Profesorado N.º 20";

    /// <summary>Dirección pública de validación que va en el QR; <c>{codigo}</c> se reemplaza por el código del documento.</summary>
    public string UrlValidacion { get; init; } = "http://localhost:3000/validar/{codigo}";

    public string UrlPara(string codigo) => UrlValidacion.Replace("{codigo}", codigo, StringComparison.Ordinal);
}
