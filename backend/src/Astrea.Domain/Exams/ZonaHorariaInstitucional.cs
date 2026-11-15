namespace Astrea.Domain.Exams;

/// <summary>
/// Hora oficial del instituto (Argentina, UTC−3). Las fechas y horas de las mesas se cargan y se
/// muestran en esta zona; los instantes absolutos se guardan y comparan en UTC.
/// </summary>
public static class ZonaHorariaInstitucional
{
    public const string IdIana = "America/Argentina/Buenos_Aires";

    public static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(IdIana);

    public static DateTimeOffset AInstante(DateOnly fecha, TimeOnly hora)
    {
        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zona.GetUtcOffset(local));
    }

    /// <summary>El día de hoy en el instituto, que no siempre coincide con el día UTC (de 21 a 24 h ya es mañana en UTC).</summary>
    public static DateOnly Hoy(DateTimeOffset ahora) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ahora, Zona).DateTime);
}
