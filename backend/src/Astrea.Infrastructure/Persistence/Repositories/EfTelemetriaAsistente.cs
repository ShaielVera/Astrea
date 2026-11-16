using Astrea.Application.Abstractions.Persistence;
using Astrea.Application.Features.Agent;
using Astrea.Domain.Agent;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Repositories;

internal sealed class EfTelemetriaAsistente(AppDbContext db) : ITelemetriaAsistente
{
    public void Agregar(ConsultaRegistrada consulta) => db.ConsultasRegistradas.Add(consulta);

    public async Task<MetricasAsistenteDto> ObtenerMetricasAsync(DateOnly desde, DateOnly hasta, int minimoFrecuencia, int maximoTemas, CancellationToken cancellationToken)
    {
        var periodo = db.ConsultasRegistradas.AsNoTracking().Where(consulta => consulta.Dia >= desde && consulta.Dia <= hasta);

        var porResultado = await periodo
            .GroupBy(consulta => consulta.Resultado)
            .Select(grupo => new { Resultado = grupo.Key, Cantidad = grupo.Count() })
            .ToListAsync(cancellationToken);
        int Cantidad(ResultadoConsulta resultado) => porResultado.FirstOrDefault(fila => fila.Resultado == resultado)?.Cantidad ?? 0;

        var propuestas = await periodo.CountAsync(consulta => consulta.ConPropuesta, cancellationToken);
        var duracion = await periodo
            .Where(consulta => consulta.Resultado != ResultadoConsulta.Error)
            .Select(consulta => (double?)consulta.DuracionMs)
            .AverageAsync(cancellationToken);

        var resumen = new ResumenConsultasDto(
            porResultado.Sum(fila => fila.Cantidad),
            Cantidad(ResultadoConsulta.Respondida),
            Cantidad(ResultadoConsulta.SinEvidencia),
            Cantidad(ResultadoConsulta.Aclaracion),
            Cantidad(ResultadoConsulta.Error),
            propuestas,
            (int)Math.Round(duracion ?? 0));

        var porDia = await periodo
            .GroupBy(consulta => consulta.Dia)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new ConsultasDelDiaDto(
                grupo.Key,
                grupo.Count(),
                grupo.Count(consulta => consulta.Resultado == ResultadoConsulta.SinEvidencia || consulta.Resultado == ResultadoConsulta.Error)))
            .ToListAsync(cancellationToken);

        var frecuentes = await periodo
            .GroupBy(consulta => consulta.Tema)
            .Where(grupo => grupo.Count() >= minimoFrecuencia)
            .OrderByDescending(grupo => grupo.Count())
            .ThenByDescending(grupo => grupo.Max(consulta => consulta.Fecha))
            .ThenBy(grupo => grupo.Key)
            .Take(maximoTemas)
            .Select(grupo => new TemaDto(grupo.Key, grupo.Count(), grupo.Max(consulta => consulta.Fecha)))
            .ToListAsync(cancellationToken);

        // Lo que el asistente no supo responder: señala qué documento falta cargar.
        var sinRespuesta = await periodo
            .Where(consulta => consulta.Resultado == ResultadoConsulta.SinEvidencia)
            .GroupBy(consulta => consulta.Tema)
            .OrderByDescending(grupo => grupo.Count())
            .ThenByDescending(grupo => grupo.Max(consulta => consulta.Fecha))
            .ThenBy(grupo => grupo.Key)
            .Take(maximoTemas)
            .Select(grupo => new TemaDto(grupo.Key, grupo.Count(), grupo.Max(consulta => consulta.Fecha)))
            .ToListAsync(cancellationToken);

        var herramientas = await periodo
            .SelectMany(consulta => consulta.Herramientas)
            .GroupBy(nombre => nombre)
            .OrderByDescending(grupo => grupo.Count())
            .ThenBy(grupo => grupo.Key)
            .Select(grupo => new CantidadDto(grupo.Key, grupo.Count()))
            .ToListAsync(cancellationToken);

        var motivos = await periodo
            .Where(consulta => consulta.Motivo != MotivoConsulta.Ninguno)
            .GroupBy(consulta => consulta.Motivo)
            .Select(grupo => new { Motivo = grupo.Key, Cantidad = grupo.Count() })
            .ToListAsync(cancellationToken);

        return new MetricasAsistenteDto(
            desde,
            hasta,
            resumen,
            porDia,
            frecuentes,
            sinRespuesta,
            herramientas,
            motivos.OrderByDescending(fila => fila.Cantidad).ThenBy(fila => fila.Motivo).Select(fila => new CantidadDto(fila.Motivo.ToString(), fila.Cantidad)).ToList());
    }

    public Task<int> EliminarAnterioresAsync(DateTimeOffset limite, CancellationToken cancellationToken) =>
        db.ConsultasRegistradas.Where(consulta => consulta.Fecha < limite).ExecuteDeleteAsync(cancellationToken);
}
