namespace Astrea.Application.Abstractions.Ai;

/// <summary>Un fragmento recuperado que el modelo puede usar y citar por su <see cref="Id"/> (F1, F2…).</summary>
public sealed record EvidenciaModelo(string Id, string Encabezado, string Contenido);

/// <summary>Un mensaje anterior de la conversación (contexto acotado para entender repreguntas).</summary>
public sealed record MensajeModelo(bool EsDelUsuario, string Texto);

/// <param name="Instrucciones">Reglas del sistema: qué puede responder y cómo.</param>
/// <param name="Historial">Últimos mensajes, del más viejo al más nuevo.</param>
/// <param name="Mensaje">La pregunta junto con las evidencias, ya delimitadas como datos.</param>
/// <param name="Evidencias">Las mismas evidencias, por separado (para validar citas y para el modelo de prueba).</param>
public sealed record ConsultaModelo(string Instrucciones, IReadOnlyList<MensajeModelo> Historial, string Mensaje, IReadOnlyList<EvidenciaModelo> Evidencias);

/// <param name="Respuesta">Texto para el usuario, con citas del tipo [F1].</param>
/// <param name="Fuentes">Ids de las evidencias que el modelo dice haber usado.</param>
/// <param name="SinEvidencia">El modelo indica que las evidencias no alcanzan para responder.</param>
public sealed record RespuestaModelo(string Respuesta, IReadOnlyList<string> Fuentes, bool SinEvidencia);

/// <summary>Puerto de salida: el modelo de lenguaje que redacta la respuesta a partir de las evidencias.</summary>
public interface IModeloLenguaje
{
    /// <exception cref="ModeloNoDisponibleException">El proveedor no está configurado, no respondió o respondió algo inválido.</exception>
    Task<RespuestaModelo> ResponderAsync(ConsultaModelo consulta, CancellationToken cancellationToken);
}

/// <summary>El modelo de lenguaje no se pudo usar. El mensaje es para el registro del servidor, no para el usuario.</summary>
public sealed class ModeloNoDisponibleException(string mensaje, Exception? inner = null) : Exception(mensaje, inner);
