using System.Text.Json;
using Astrea.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Seed;

/// <summary>Resultado de la siembra. Las advertencias indican planes cuyas correlatividades no se pudieron cargar.</summary>
public sealed record ResultadoSiembra(int CarrerasAgregadas, int PlanesConCorrelatividades, IReadOnlyList<string> Advertencias);

/// <summary>
/// Carga las carreras, planes de estudio y correlatividades del ISP N.º 20 desde
/// catalogo-isp20.json y correlatividades-isp20.json. Es idempotente:
/// una carrera cuyo código ya existe no se toca y un plan que ya tiene correlatividades tampoco
/// (Bedelía puede haberlos editado).
/// </summary>
public static class CatalogoInstitucionalSeeder
{
    private const string RecursoCatalogo = "Astrea.Infrastructure.Persistence.Seed.catalogo-isp20.json";
    private const string RecursoCorrelatividades = "Astrea.Infrastructure.Persistence.Seed.correlatividades-isp20.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public static async Task<ResultadoSiembra> SembrarAsync(AppDbContext db, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var catalogo = await LeerAsync<CatalogoJson>(RecursoCatalogo, cancellationToken);
        var carrerasAgregadas = await SembrarCarrerasAsync(db, catalogo, ahora, cancellationToken);

        var correlatividades = await LeerAsync<CorrelatividadesJson>(RecursoCorrelatividades, cancellationToken);
        var advertencias = new List<string>();
        var planesConCorrelatividades = 0;

        foreach (var (codigo, destinos) in correlatividades.Carreras)
        {
            var resolucion = catalogo.Carreras.Single(carrera => carrera.Codigo == codigo).Resolucion;
            if (await SembrarCorrelatividadesAsync(db, codigo, resolucion, destinos, advertencias, cancellationToken))
            {
                planesConCorrelatividades++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoSiembra(carrerasAgregadas, planesConCorrelatividades, advertencias);
    }

    private static async Task<int> SembrarCarrerasAsync(AppDbContext db, CatalogoJson catalogo, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// Carga las correlatividades del plan oficial de la carrera si todavía no tiene ninguna.
    /// Si algún nombre no coincide con una materia del plan (por ejemplo, porque Bedelía la renombró),
    /// no carga nada de ese plan y deja una advertencia.
    /// </summary>
    private static async Task<bool> SembrarCorrelatividadesAsync(
        AppDbContext db,
        string codigoCarrera,
        string resolucion,
        IReadOnlyDictionary<string, DestinoJson> destinos,
        List<string> advertencias,
        CancellationToken cancellationToken)
    {
        var plan = await db.PlanesEstudio
            .Include(plan => plan.Materias)
            .Include(plan => plan.Correlatividades)
            .AsSplitQuery()
            .Where(plan => plan.CodigoVersion == resolucion
                && db.Carreras.Any(carrera => carrera.Id == plan.CarreraId && carrera.Codigo == codigoCarrera))
            .SingleOrDefaultAsync(cancellationToken);

        if (plan is null || plan.Correlatividades.Count > 0)
        {
            return false;
        }

        // Los nombres repetidos dentro del plan (ej. "Itinerarios por el Mundo de la Cultura") no se pueden usar como requisito.
        var idsMaterias = plan.Materias.Select(planMateria => planMateria.MateriaId).ToList();
        var nombres = await db.Materias
            .Where(materia => idsMaterias.Contains(materia.Id))
            .Select(materia => new { materia.Id, materia.Nombre })
            .ToListAsync(cancellationToken);
        var planMateriaPorNombre = nombres
            .GroupBy(materia => materia.Nombre, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() == 1)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => plan.Materias.Single(planMateria => planMateria.MateriaId == grupo.Single().Id).Id,
                StringComparer.Ordinal);

        var reglas = new List<(Guid Destino, TipoCorrelatividad Tipo, Guid? Requisito, CondicionRequisito Condicion, int? Grupo, string? Descripcion)>();
        var faltantes = new SortedSet<string>(StringComparer.Ordinal);

        Guid? Resolver(string nombre)
        {
            if (planMateriaPorNombre.TryGetValue(nombre, out var id))
            {
                return id;
            }

            faltantes.Add(nombre);
            return null;
        }

        foreach (var (nombreDestino, tipos) in destinos)
        {
            var destino = Resolver(nombreDestino);
            foreach (var (tipo, requisitos) in new[] { (TipoCorrelatividad.Cursar, tipos.Cursar), (TipoCorrelatividad.Rendir, tipos.Rendir) })
            {
                if (requisitos is null || destino is null)
                {
                    continue;
                }

                var grupo = 0;
                foreach (var (condicion, entradas) in new[] { (CondicionRequisito.Regularizada, requisitos.Regularizadas), (CondicionRequisito.Aprobada, requisitos.Aprobadas) })
                {
                    foreach (var entrada in entradas ?? [])
                    {
                        var alternativas = entrada.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        int? numeroGrupo = alternativas.Length > 1 ? ++grupo : null;
                        foreach (var alternativa in alternativas)
                        {
                            reglas.Add((destino.Value, tipo, Resolver(alternativa) ?? Guid.Empty, condicion, numeroGrupo, null));
                        }
                    }
                }

                foreach (var descripcion in requisitos.Otros ?? [])
                {
                    reglas.Add((destino.Value, tipo, null, CondicionRequisito.Aprobada, null, descripcion));
                }
            }
        }

        if (faltantes.Count > 0)
        {
            advertencias.Add($"{codigoCarrera}: no se cargaron las correlatividades porque estas materias no están en el plan {resolucion}: {string.Join(", ", faltantes)}.");
            return false;
        }

        foreach (var regla in reglas)
        {
            plan.AgregarCorrelatividad(Guid.CreateVersion7(), regla.Destino, regla.Tipo, regla.Requisito, regla.Condicion, regla.Grupo, regla.Descripcion);
        }

        return true;
    }

    private static (string Nombre, Cuatrimestre Cuatrimestre) Interpretar(string entrada)
    {
        var partes = entrada.Split('@');
        var cuatrimestre = partes.Length == 2 ? (Cuatrimestre)int.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture) : Cuatrimestre.Anual;
        return (partes[0].Trim(), cuatrimestre);
    }

    private static async Task<T> LeerAsync<T>(string recurso, CancellationToken cancellationToken)
    {
        await using var stream = typeof(CatalogoInstitucionalSeeder).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"No se encontró el recurso {recurso}.");

        return await JsonSerializer.DeserializeAsync<T>(stream, OpcionesJson, cancellationToken)
            ?? throw new InvalidOperationException($"El recurso {recurso} está vacío.");
    }

    private sealed record CatalogoJson(IReadOnlyList<CarreraJson> Carreras);

    private sealed record CarreraJson(string Codigo, string Nombre, int DuracionAnios, string Resolucion, IReadOnlyList<IReadOnlyList<string>> Anios);

    private sealed record CorrelatividadesJson(IReadOnlyDictionary<string, IReadOnlyDictionary<string, DestinoJson>> Carreras);

    private sealed record DestinoJson(RequisitosJson? Cursar, RequisitosJson? Rendir);

    private sealed record RequisitosJson(IReadOnlyList<string>? Regularizadas, IReadOnlyList<string>? Aprobadas, IReadOnlyList<string>? Otros);
}
