using System.Text.Json;

namespace Astrea.Application.Abstractions.Ai;

public enum TipoParametro
{
    Texto,

    /// <summary>Fecha AAAA-MM-DD (se envía como texto y la valida el backend).</summary>
    Fecha,

    Booleano,

    ListaDeTextos,

    /// <summary>Lista de objetos con los <see cref="ParametroHerramienta.Campos"/> indicados.</summary>
    ListaDeObjetos,
}

/// <param name="Valores">Si no es null, los únicos valores admitidos (un enum).</param>
/// <param name="Campos">Para <see cref="TipoParametro.ListaDeObjetos"/>: los campos de cada objeto.</param>
public sealed record ParametroHerramienta(
    string Nombre,
    TipoParametro Tipo,
    string Descripcion,
    bool Obligatorio,
    IReadOnlyList<string>? Valores = null,
    IReadOnlyList<ParametroHerramienta>? Campos = null);

/// <summary>Lo que el modelo sabe de una herramienta: nombre, para qué sirve y qué argumentos lleva.</summary>
public sealed record DefinicionHerramienta(string Nombre, string Descripcion, IReadOnlyList<ParametroHerramienta> Parametros);

/// <summary>Un paso de la conversación con el modelo.</summary>
public abstract record TurnoAgente;

public sealed record TurnoUsuario(string Texto) : TurnoAgente;

/// <summary>Una respuesta anterior del asistente (historial).</summary>
public sealed record TurnoModelo(string Texto) : TurnoAgente;

/// <summary>
/// Herramientas que el modelo pidió usar. <paramref name="Original"/> es el contenido tal como lo devolvió el proveedor:
/// algunos (Gemini) exigen recibirlo sin cambios en el pedido siguiente.
/// </summary>
public sealed record TurnoLlamadas(IReadOnlyList<LlamadaHerramienta> Llamadas, object? Original = null) : TurnoAgente;

/// <summary>Lo que devolvió el backend al ejecutar (o rechazar) cada llamada, en el mismo orden.</summary>
public sealed record TurnoResultados(IReadOnlyList<ResultadoHerramienta> Resultados) : TurnoAgente;

public sealed record LlamadaHerramienta(string Nombre, JsonElement Argumentos);

/// <param name="Contenido">Objeto serializable a JSON con el resultado (o un error) para el modelo.</param>
public sealed record ResultadoHerramienta(string Nombre, object Contenido);

public sealed record ConsultaAgente(string Instrucciones, IReadOnlyList<DefinicionHerramienta> Herramientas, IReadOnlyList<TurnoAgente> Turnos);

/// <summary>
/// Puerto de salida: el modelo de lenguaje. En cada paso solo PROPONE llamadas a herramientas (incluida "responder",
/// que termina el turno); el backend valida y ejecuta cada una. El modelo nunca toca datos directamente.
/// </summary>
public interface IModeloLenguaje
{
    /// <summary>Las llamadas que propone el modelo. Una lista vacía significa que no supo cómo seguir.</summary>
    /// <exception cref="ModeloNoDisponibleException">El proveedor no está configurado, no respondió o respondió algo inválido.</exception>
    Task<TurnoLlamadas> SiguientePasoAsync(ConsultaAgente consulta, CancellationToken cancellationToken);
}

/// <summary>El modelo de lenguaje no se pudo usar. El mensaje es para el registro del servidor, no para el usuario.</summary>
public sealed class ModeloNoDisponibleException(string mensaje, Exception? inner = null) : Exception(mensaje, inner);
