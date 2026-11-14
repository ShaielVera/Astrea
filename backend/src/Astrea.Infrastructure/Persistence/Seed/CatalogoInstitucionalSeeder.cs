using System.Text.Json;
using Astrea.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Seed;

/// <summary>
/// Carga las carreras y planes de estudio del ISP N.º 20 desde catalogo-isp20.json.
/// Es idempotente: una carrera cuyo código ya existe no se toca (Bedelía puede haberla editado).
/// </summary>
public static class CatalogoInstitucionalSeeder
{
    private const string Recurso = "Astrea.Infrastructure.Persistence.Seed.catalogo-isp20.json";

    public static async Task<int> SembrarAsync(AppDbContext db, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var catalogo = await LeerCatalogoAsync(cancellationToken);
        var existentes = await db.Carreras.Select(carrera => carrera.Codigo).ToListAsync(cancellationToken);
        var agregadas = 0;

        foreach (var datos in catalogo.Carreras.Where(datos => !existentes.Contains(datos.Codigo)))
        {
            var carrera = Carrera.Crear(Guid.CreateVersion7(), datos.Codigo, datos.Nombre, datos.DuracionAnios, ahora);
            var plan = PlanEstudio.Crear(Guid.CreateVersion7(), carrera, datos.Resolucion, $"Plan de estudios {datos.Resolucion}", null, null);
            db.Carreras.Add(carrera);
            db.PlanesEstudio.Add(plan);

            var secuencia = 1;
            for (var anio = 1; anio <= datos.Anios.Count; anio++)
            {
                foreach (var entrada in datos.Anios[anio - 1])
                {
                    var (nombre, cuatrimestre) = Interpretar(entrada);
                    var materia = Materia.Crear(Guid.CreateVersion7(), $"{carrera.Codigo}-{secuencia++:000}", nombre);
                    db.Materias.Add(materia);
                    plan.AgregarMateria(Guid.CreateVersion7(), materia, anio, cuatrimestre, cargaHorariaSemanal: null, obligatoria: true);
                }
            }

            agregadas++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return agregadas;
    }

    private static (string Nombre, Cuatrimestre Cuatrimestre) Interpretar(string entrada)
    {
        var partes = entrada.Split('@');
        var cuatrimestre = partes.Length == 2 ? (Cuatrimestre)int.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture) : Cuatrimestre.Anual;
        return (partes[0].Trim(), cuatrimestre);
    }

    private static async Task<CatalogoJson> LeerCatalogoAsync(CancellationToken cancellationToken)
    {
        await using var stream = typeof(CatalogoInstitucionalSeeder).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"No se encontró el recurso {Recurso}.");

        return await JsonSerializer.DeserializeAsync<CatalogoJson>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            ?? throw new InvalidOperationException("El catálogo institucional está vacío.");
    }

    private sealed record CatalogoJson(IReadOnlyList<CarreraJson> Carreras);

    private sealed record CarreraJson(string Codigo, string Nombre, int DuracionAnios, string Resolucion, IReadOnlyList<IReadOnlyList<string>> Anios);
}
