using Astrea.Application.Abstractions.Documents;

namespace Astrea.Infrastructure.Documents;

/// <summary>
/// Almacenamiento en una carpeta privada del servidor, fuera de cualquier ruta publicada por la web:
/// los archivos solo salen a través de la API, que controla quién puede descargarlos.
/// Se puede reemplazar por Firebase Storage implementando <see cref="IFileStorage"/>.
/// </summary>
internal sealed class CarpetaPrivadaStorage(string carpetaRaiz) : IFileStorage
{
    private readonly string _raiz = Path.GetFullPath(carpetaRaiz);

    public async Task GuardarAsync(string ruta, byte[] contenido, CancellationToken cancellationToken)
    {
        var destino = RutaSegura(ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        // Se escribe a un temporal y se renombra: nunca queda un PDF a medio escribir.
        var temporal = $"{destino}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temporal, contenido, cancellationToken);
        File.Move(temporal, destino, overwrite: true);
    }

    public async Task<byte[]?> LeerAsync(string ruta, CancellationToken cancellationToken)
    {
        var origen = RutaSegura(ruta);
        return File.Exists(origen) ? await File.ReadAllBytesAsync(origen, cancellationToken) : null;
    }

    public Task EliminarAsync(string ruta, CancellationToken cancellationToken)
    {
        var archivo = RutaSegura(ruta);
        if (File.Exists(archivo))
        {
            File.Delete(archivo);
        }

        return Task.CompletedTask;
    }

    /// <summary>Impide salir de la carpeta raíz con rutas como "../../otra".</summary>
    private string RutaSegura(string ruta)
    {
        var completa = Path.GetFullPath(Path.Combine(_raiz, ruta));
        if (!completa.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta del archivo sale de la carpeta privada.");
        }

        return completa;
    }
}
