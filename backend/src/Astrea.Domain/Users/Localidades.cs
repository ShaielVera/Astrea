using System.Globalization;
using System.Text;

namespace Astrea.Domain.Users;

/// <summary>Una ciudad, pueblo o paraje, con su departamento y otros nombres con que se la conoce (también sirven para buscarla).</summary>
public sealed record Localidad(string Nombre, string Departamento, IReadOnlyList<string> OtrosNombres);

/// <summary>
/// Localidades y parajes a 100 km o menos de San Justo, que se eligen en "Mi perfil". Se eligen de la lista (no se escriben a mano)
/// para que "San Justo" no termine guardado como "san justo", "San justo" o "S. Justo".
/// Fuente: asentamientos de Santa Fe del Servicio de Normalización de Datos Geográficos (Georef, datos.gob.ar), con la distancia
/// calculada desde el centro de San Justo. Si un nombre se repite en dos departamentos, se aclara el departamento entre paréntesis.
/// Para sumar una, se agrega acá.
/// </summary>
public static class Localidades
{
    public static readonly IReadOnlyList<Localidad> Todas = Armar(
        ("Castellanos", [("Ataliva", []), ("Aurelia Norte", []), ("Bella Italia", []), ("Campo Giacosa", []), ("Campo La Nieva", []), ("Campo Ristorto", []), ("Campo Rubiolo", []), ("Cañada El Cisne", []), ("Colonia La Manuelita", []), ("Colonia Raquel", []), ("Colonia Reina Margarita", []), ("Colonia Roccia", []), ("Galisteo", []), ("Humberto Primo", []), ("La Fronterita", []), ("Lehmann", []), ("Mauá", []), ("Nueva Lehmann", []), ("Rafaela", []), ("Rodeo Chico", []), ("Sunchales", []), ("Tacural", []), ("Tres Colonias", []), ("Virginia", [])]),
        ("Garay", [("Algarrobos", []), ("Barrio SUPCE", []), ("Campo del Medio", []), ("Cayastá", []), ("Colonia Helvecia Oeste", []), ("Colonia Helvecia Sur", []), ("Colonia Mascías", []), ("Colonia Norte", []), ("Colonia San Joaquín", []), ("Cuatro Bocas", []), ("El Laurel", []), ("Helvecia", []), ("Las Cañas", []), ("Los Cerrillos", []), ("Los Zapallos", []), ("Parque El Ceibal", []), ("Parque El Timbó", []), ("Saladero Mariano Cabal", []), ("San Joaquín", []), ("Santa Rosa de Calchines", []), ("Vuelta del Dorado", [])]),
        ("La Capital", [("Angel Gallardo", []), ("Aromos", []), ("Arroyo Aguiar", []), ("Arroyo Leyes", []), ("Balneario Esperanza (La Capital)", []), ("Balneario Laguna Paiva", []), ("Cabal", []), ("Campo Andino", []), ("Candioti", []), ("Emilia", []), ("Estación Emilia", []), ("Iriondo", []), ("Laguna Paiva", []), ("Llambi Campbell", []), ("Los Bretes", []), ("Manucho", []), ("Monte Vera", []), ("Nelson", []), ("Paraje Cabal", []), ("Paraje Chaco Chico", []), ("Paraje La Costa", []), ("Recreo", []), ("Rincón de Ávila", []), ("Rincón Potrero", []), ("San José del Rincón", []), ("Santa Fe", []), ("Santo Tomé", []), ("Setúbal", []), ("Villa Laura", [])]),
        ("Las Colonias", [("Balneario Esperanza (Las Colonias)", []), ("Cavour", []), ("Colonia Cavour", []), ("Colonia La Rinconada", []), ("Colonia Larrechea", []), ("Colonia Nueva", []), ("Colonia Nueva Centro", []), ("Colonia Nueva Norte", []), ("Colonia Pujol", []), ("Colonia Rivadavia", []), ("Colonia San José (Las Colonias)", []), ("Cululú", []), ("El Bosque", []), ("Elisa", []), ("Empalme San Carlos", []), ("Esperanza", []), ("Estación Humboldt", []), ("Felicia", []), ("Franck", []), ("Grutly", []), ("Grutly Norte", []), ("Hipatía", []), ("Humboldt", []), ("Ingeniero Boassi", []), ("Ituzaingó", []), ("Jacinto L. Aráuz", []), ("La Pelada", []), ("La Ramada", []), ("La Teresita", []), ("Las Tunas", []), ("María Luisa", []), ("Nuevo Torino", []), ("Pilar", []), ("Progreso", []), ("Providencia", []), ("Pujato Norte", []), ("Rincón del Pintado", []), ("San Jerónimo Norte", []), ("San José", []), ("San Wendelino", []), ("Santa María Norte", []), ("Santo Domingo", []), ("Sarmiento", []), ("Soutomayor", []), ("Villa Paso de las Piedras", [])]),
        ("San Cristóbal", [("Aguará Grande", []), ("Campo Massoni", []), ("Capivara", []), ("Colonia Adolfo Alsina", []), ("Colonia Alcorta", []), ("Colonia Clara", []), ("Colonia El Algarrobal", []), ("Colonia Iztueta", []), ("Colonia Muchtnik", []), ("Colonia Torres", []), ("Colonia Wavelberg", []), ("Constanza", []), ("Estación Rincón del Quebracho", []), ("Estación Santurce", []), ("La Cabral", []), ("La Clara (San Cristóbal)", []), ("La Frontera", []), ("La Lucila", []), ("Las Palmeras", []), ("María Eugenia", []), ("Moisés Ville", []), ("Ñanducita", []), ("Palacios", []), ("Petronila", []), ("Portugalete", []), ("Rincón del Quebracho", []), ("San Cristóbal", []), ("San Francisco", []), ("Santurce", []), ("Soledad", ["Villa Soledad"]), ("Villa Saralegui", []), ("Vizcacheras", [])]),
        ("San Javier", [("Cacique Ariacaiquín", []), ("Colonia California", []), ("Colonia Criolla", []), ("Colonia Francesa", []), ("Colonia San Ignacio", []), ("Colonia San José (San Javier)", []), ("Colonia Teresa", []), ("Colonia Yatay", []), ("La Brava", []), ("Los Jacintos", []), ("San Javier", []), ("Villa San Antonio", [])]),
        ("San Justo", [("Angeloni", []), ("Arrascaeta", []), ("Asunción María", []), ("Campo Pichuco", []), ("Cayastacito", ["Caastacito"]), ("Colonia Angeloni", []), ("Colonia Dolores", []), ("Colonia El Fortín", []), ("Colonia La Blanca", []), ("Colonia La Hosca", []), ("Colonia La Mora", []), ("Colonia Ovejita", []), ("Colonia Sol de Mayo", []), ("Estación Vera Mujica", []), ("Esther", ["Colonia Esther"]), ("Gobernador Crespo", []), ("La Blanca", []), ("La Cabaña", []), ("La Camila", []), ("La Clara (San Justo)", []), ("La Clorinda", []), ("La Criolla", []), ("La Negra", []), ("La Nevada", []), ("La Penca y Caraguatá", []), ("La Pepita", []), ("La Rinconada", []), ("Laguna del Plata", []), ("Laguna del Platero", []), ("Los Saladillos", []), ("Luciano Leiva", []), ("Marcelino Escalada", []), ("Miguel Escalada", []), ("Naré", []), ("Ñandubay", []), ("Paikín", []), ("Pedro Gómez Cello", []), ("Ramayón", []), ("San Bernardo", []), ("San Juan", []), ("San Justo", []), ("San Martín Norte", []), ("Silva", []), ("Vera Mujica", []), ("Vera y Pintado", []), ("Videla", []), ("Villa Lastenia", [])]),
        ("Vera", [("Colonia Los Galpones", []), ("El Aromal", []), ("El Cristal", []), ("La Hosca", []), ("San Pedro", [])]));

    private static readonly Dictionary<string, Localidad> PorClave = Todas
        .SelectMany(localidad => localidad.OtrosNombres.Prepend(localidad.Nombre).Select(nombre => (Clave: Clave(nombre), Localidad: localidad)))
        .DistinctBy(par => par.Clave)
        .ToDictionary(par => par.Clave, par => par.Localidad);

    /// <summary>La localidad tal como figura en la lista, aunque llegue con otras mayúsculas, sin tildes o con otro de sus nombres. Null si no está.</summary>
    public static Localidad? Buscar(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? null : PorClave.GetValueOrDefault(Clave(nombre));

    /// <summary>"  san  JUSTO " y "Sán Justo" dan la misma clave: minúsculas, sin tildes y con un solo espacio.</summary>
    private static string Clave(string texto)
    {
        var sinTildes = new StringBuilder();
        foreach (var caracter in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinTildes.Append(char.ToLowerInvariant(caracter));
            }
        }

        return string.Join(' ', sinTildes.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<Localidad> Armar(params (string Departamento, (string Nombre, string[] OtrosNombres)[] Localidades)[] departamentos) =>
        departamentos
            .SelectMany(departamento => departamento.Localidades.Select(l => new Localidad(l.Nombre, departamento.Departamento, l.OtrosNombres)))
            .OrderBy(localidad => Clave(localidad.Nombre), StringComparer.Ordinal)
            .ToList();
}
