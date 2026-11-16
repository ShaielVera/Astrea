using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Astrea.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace Astrea.Infrastructure.Persistence.Seed;

/// <summary>Resultado de cargar las mesas publicadas por el instituto.</summary>
public sealed record ResultadoMesas(int MesasCreadas, int MesasExistentes, IReadOnlyList<string> Advertencias);

/// <summary>
/// Carga las mesas de examen que el instituto publicó en su sitio (un PDF por carrera), transcriptas en
/// <c>mesas-isp20.json</c>. Cada fila se cruza con su materia del plan vigente: por nombre normalizado (sin tildes, signos
/// ni aclaraciones entre paréntesis) y, si el instituto usa otro nombre, por la tabla de equivalencias del archivo.
/// Las mesas se crean con las mismas reglas del dominio que usa Secretaría y quedan publicadas.
/// Es idempotente: una mesa que ya existe (misma materia, turno y llamado) no se vuelve a crear.
/// </summary>
public static partial class MesasInstitucionalesSeeder
{
    private const string Recurso = "Astrea.Infrastructure.Persistence.Seed.mesas-isp20.json";

    public static async Task<ResultadoMesas> SembrarAsync(AppDbContext db, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        await using var stream = typeof(MesasInstitucionalesSeeder).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"No se encontró el recurso {Recurso}.");
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var raiz = json.RootElement;
        var turno = raiz.GetProperty("turno").GetString()!;
        var anio = raiz.GetProperty("anio").GetInt32();
        var equivalencias = raiz.TryGetProperty("equivalencias", out var eq) ? eq : default;

        // Materias de los planes vigentes de cada carrera, con su año en el plan.
        var materias = await (
                from planMateria in db.PlanMaterias
                join materia in db.Materias on planMateria.MateriaId equals materia.Id
                join plan in db.PlanesEstudio on planMateria.PlanEstudioId equals plan.Id
                join carrera in db.Carreras on plan.CarreraId equals carrera.Id
                where plan.Activo
                orderby planMateria.Orden
                select new MateriaDelPlan(carrera.Codigo, planMateria.Id, planMateria.AnioEnPlan, materia.Nombre))
            .ToListAsync(cancellationToken);

        var existentes = (await db.MesasExamen
                .Where(mesa => mesa.Turno == turno)
                .Select(mesa => new { mesa.PlanMateriaId, mesa.Llamado })
                .ToListAsync(cancellationToken))
            .Select(mesa => (mesa.PlanMateriaId, mesa.Llamado))
            .ToHashSet();

        var advertencias = new List<string>();
        var creadas = 0;
        var yaEstaban = 0;
        foreach (var carrera in raiz.GetProperty("carreras").EnumerateObject())
        {
            var delPlan = materias.Where(materia => materia.Carrera == carrera.Name).ToList();
            foreach (var fila in carrera.Value.EnumerateArray())
            {
                var anioCursada = fila[0].GetInt32();
                var publicada = fila[1].GetString()!;
                var destinos = Destinos(equivalencias, carrera.Name, publicada);
                foreach (var destino in destinos)
                {
                    var planMateriaId = Buscar(delPlan, anioCursada, destino);
                    if (planMateriaId is null)
                    {
                        advertencias.Add($"{carrera.Name}: no se encontró la materia \"{destino}\" en el plan.");
                        continue;
                    }

                    for (var llamado = 1; llamado <= 2; llamado++)
                    {
                        if (fila[llamado + 1].ValueKind == JsonValueKind.Null)
                        {
                            continue;
                        }

                        if (existentes.Contains((planMateriaId.Value, llamado)))
                        {
                            yaEstaban++;
                            continue;
                        }

                        var fecha = Fecha(fila[llamado + 1].GetString()!, anio);
                        var hora = TimeOnly.ParseExact(fila[4].GetString()!, "HH:mm", CultureInfo.InvariantCulture);
                        if (ZonaHorariaInstitucional.AInstante(fecha, hora) <= ahora)
                        {
                            advertencias.Add($"{carrera.Name}: la mesa de \"{publicada}\" del {fecha:dd/MM} ya pasó; no se carga.");
                            continue;
                        }

                        var tribunal = Tribunal(llamado == 2 && fila.GetArrayLength() > 6 && fila[6].ValueKind == JsonValueKind.String
                            ? fila[6].GetString()!
                            : fila[5].GetString()!);
                        var observacion = fila.GetArrayLength() > 7 ? fila[7].GetString() : null;

                        var mesa = MesaExamen.Crear(
                            Guid.CreateVersion7(),
                            planMateriaId.Value,
                            new DatosMesa(turno, llamado, fecha, hora, Aula: null, tribunal, InscripcionDesde: null, InscripcionHasta: null, observacion),
                            ahora);
                        mesa.Publicar(ahora);
                        db.MesasExamen.Add(mesa);
                        existentes.Add((planMateriaId.Value, llamado));
                        creadas++;
                    }
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoMesas(creadas, yaEstaban, advertencias);
    }

    /// <summary>"CALVO M – PEDRAZZOLI,F-MIÑOZ,A" → ["Calvo, M", "Pedrazzoli, F", "Miñoz, A"]. Lo que va entre paréntesis se conserva.</summary>
    public static IReadOnlyList<string> Tribunal(string publicado) =>
        SeparadorTribunal().Split(publicado)
            .Select(integrante => integrante.Trim().TrimEnd(',').Trim())
            .Where(integrante => integrante.Length > 0)
            .Select(Persona)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Compara nombres de materia sin tildes, mayúsculas, signos ni aclaraciones entre paréntesis.</summary>
    public static string Normalizar(string nombre)
    {
        var sinParentesis = Parentesis().Replace(nombre, " ");
        var sinTildes = new string(sinParentesis.Normalize(NormalizationForm.FormD)
            .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Espacios().Replace(NoAlfanumerico().Replace(sinTildes.ToUpperInvariant(), " "), " ").Trim();
    }

    private static string Persona(string publicado)
    {
        var parentesis = Parentesis().Match(publicado);
        var nombre = Parentesis().Replace(publicado, string.Empty).Trim().TrimEnd('.', ',').Trim();
        string apellido;
        var iniciales = string.Empty;
        var coma = nombre.IndexOf(',', StringComparison.Ordinal);
        if (coma >= 0)
        {
            apellido = nombre[..coma];
            iniciales = nombre[(coma + 1)..];
        }
        else
        {
            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ultima = partes.Length > 1 ? partes[^1].Replace(".", string.Empty, StringComparison.Ordinal) : string.Empty;
            if (ultima.Length is > 0 and <= 3)
            {
                apellido = string.Join(' ', partes[..^1]);
                iniciales = ultima;
            }
            else
            {
                apellido = nombre;
            }
        }

        apellido = CultureInfo.GetCultureInfo("es-AR").TextInfo.ToTitleCase(apellido.Trim().ToLowerInvariant());
        iniciales = iniciales.Replace(".", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        var resultado = iniciales.Length switch
        {
            0 => apellido,
            <= 3 => $"{apellido}, {iniciales.ToUpperInvariant()}",
            _ => $"{apellido}, {CultureInfo.GetCultureInfo("es-AR").TextInfo.ToTitleCase(iniciales.ToLowerInvariant())}",
        };
        return parentesis.Success ? $"{resultado} ({Persona(parentesis.Value.Trim('(', ')', ' '))})" : resultado;
    }

    private static IReadOnlyList<string> Destinos(JsonElement equivalencias, string carrera, string publicada)
    {
        if (equivalencias.ValueKind == JsonValueKind.Object
            && equivalencias.TryGetProperty(carrera, out var deLaCarrera)
            && deLaCarrera.TryGetProperty(publicada, out var destino))
        {
            return destino.ValueKind == JsonValueKind.Array
                ? destino.EnumerateArray().Select(elemento => elemento.GetString()!).ToList()
                : [destino.GetString()!];
        }

        return [publicada];
    }

    /// <summary>La materia del plan con ese nombre (en ese año, si se indica). "Nombre#2" elige la segunda con ese nombre.</summary>
    private static Guid? Buscar(IReadOnlyList<MateriaDelPlan> delPlan, int anioCursada, string destino)
    {
        var partes = destino.Split('#');
        var nombre = Normalizar(partes[0]);
        var candidatas = delPlan
            .Where(materia => Normalizar(materia.Nombre) == nombre && (anioCursada == 0 || materia.Anio == anioCursada))
            .ToList();
        if (partes.Length == 2)
        {
            var posicion = int.Parse(partes[1], CultureInfo.InvariantCulture);
            return candidatas.Count >= posicion ? candidatas[posicion - 1].PlanMateriaId : null;
        }

        return candidatas.Count == 1 ? candidatas[0].PlanMateriaId : null;
    }

    /// <summary>"24/11" del turno 2026 → 24/11/2026. Un mes de la primera mitad del año corresponde al año siguiente.</summary>
    private static DateOnly Fecha(string diaMes, int anio)
    {
        var partes = diaMes.Split('/');
        var dia = int.Parse(partes[0], CultureInfo.InvariantCulture);
        var mes = int.Parse(partes[1], CultureInfo.InvariantCulture);
        return new DateOnly(mes <= 6 ? anio + 1 : anio, mes, dia);
    }

    // Guion, guion largo o raya entre integrantes; no corta lo que está entre paréntesis.
    [GeneratedRegex(@"\s*[-–—]\s*(?![^()]*\))", RegexOptions.CultureInvariant)]
    private static partial Regex SeparadorTribunal();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex Parentesis();

    [GeneratedRegex("[^A-Z0-9 ]", RegexOptions.CultureInvariant)]
    private static partial Regex NoAlfanumerico();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Espacios();

    private sealed record MateriaDelPlan(string Carrera, Guid PlanMateriaId, int Anio, string Nombre);
}
