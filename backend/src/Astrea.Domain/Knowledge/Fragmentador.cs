using System.Text;
using System.Text.RegularExpressions;

namespace Astrea.Domain.Knowledge;

/// <summary>Texto de una página del PDF, tal como sale del extractor.</summary>
public sealed record PaginaTexto(int Numero, string Texto);

/// <summary>Pedazo de texto listo para generar su embedding.</summary>
public sealed record FragmentoTexto(int Numero, string Contenido, string? Seccion, int PaginaDesde, int PaginaHasta);

/// <summary>
/// Divide el texto de un documento en fragmentos para el RAG:
/// respeta encabezados (Capítulo, Título, Artículo…) y párrafos, corta por oraciones cuando un párrafo es muy largo
/// y repite el final del fragmento anterior (solapamiento) para no perder el contexto en los cortes.
/// </summary>
public static partial class Fragmentador
{
    /// <summary>Caracteres por fragmento (~300 tokens). Suficiente para un artículo de reglamento completo.</summary>
    public const int TamanioMaximo = 1200;

    /// <summary>Alrededor del 12 % del tamaño.</summary>
    public const int Solapamiento = 150;

    private const int LargoMaximoEncabezado = 90;

    public static IReadOnlyList<FragmentoTexto> Fragmentar(IEnumerable<PaginaTexto> paginas, int tamanioMaximo = TamanioMaximo, int solapamiento = Solapamiento)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tamanioMaximo, 100);
        ArgumentOutOfRangeException.ThrowIfNegative(solapamiento);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(solapamiento, tamanioMaximo / 2);

        var armado = new Armado(tamanioMaximo, solapamiento);
        foreach (var bloque in paginas.OrderBy(pagina => pagina.Numero).SelectMany(Bloques))
        {
            if (bloque.EsEncabezado)
            {
                armado.Encabezado(bloque.Texto, bloque.Pagina);
            }
            else
            {
                foreach (var pieza in Piezas(bloque.Texto, tamanioMaximo - solapamiento - 5))
                {
                    armado.Agregar(pieza, bloque.Pagina, bloque.EmpiezaParrafo);
                }
            }
        }

        armado.Cerrar();
        return armado.Fragmentos;
    }

    /// <summary>Normaliza el texto de la página y lo separa en encabezados y párrafos.</summary>
    private static IEnumerable<Bloque> Bloques(PaginaTexto pagina)
    {
        var texto = pagina.Texto.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace(' ', ' ');

        // "regla-\nmento" → "reglamento" (palabras cortadas al final de la línea).
        texto = PalabraCortada().Replace(texto, "$1$2");

        var parrafo = new StringBuilder();
        string? anterior = null;
        var bloques = new List<Bloque>();

        void CerrarParrafo()
        {
            if (parrafo.Length > 0)
            {
                bloques.Add(new Bloque(parrafo.ToString(), pagina.Numero, EsEncabezado: false, EmpiezaParrafo: true));
                parrafo.Clear();
            }
        }

        foreach (var crudo in texto.Split('\n'))
        {
            var linea = Espacios().Replace(crudo, " ").Trim();
            if (linea.Length == 0)
            {
                CerrarParrafo();
                anterior = null;
                continue;
            }

            if (EsEncabezado(linea))
            {
                CerrarParrafo();
                bloques.Add(new Bloque(linea, pagina.Numero, EsEncabezado: true, EmpiezaParrafo: true));
                anterior = null;
                continue;
            }

            // Un artículo o una línea que empieza después de un punto final arranca un párrafo nuevo.
            if (InicioDeArticulo().IsMatch(linea) || (anterior is not null && FinDeOracion().IsMatch(anterior) && char.IsUpper(linea[0]) && anterior.Length < 60))
            {
                CerrarParrafo();
            }

            if (parrafo.Length > 0)
            {
                parrafo.Append(' ');
            }

            parrafo.Append(linea);
            anterior = linea;
        }

        CerrarParrafo();
        return bloques;
    }

    private static bool EsEncabezado(string linea)
    {
        if (linea.Length > LargoMaximoEncabezado)
        {
            return false;
        }

        if (EncabezadoConPalabraClave().IsMatch(linea))
        {
            return true;
        }

        // Línea corta en mayúsculas sin punto final: "RÉGIMEN DE CORRELATIVIDADES".
        var letras = linea.Where(char.IsLetter).ToList();
        return letras.Count >= 4 && letras.All(char.IsUpper) && !linea.EndsWith('.');
    }

    /// <summary>Corta un párrafo largo por oraciones y, si una oración sola no entra, por palabras.</summary>
    private static IEnumerable<string> Piezas(string parrafo, int maximo)
    {
        if (parrafo.Length <= maximo)
        {
            yield return parrafo;
            yield break;
        }

        foreach (var oracion in Oraciones().Split(parrafo).Where(o => o.Length > 0))
        {
            if (oracion.Length <= maximo)
            {
                yield return oracion;
                continue;
            }

            var actual = new StringBuilder();
            foreach (var palabra in oracion.Split(' '))
            {
                if (actual.Length > 0 && actual.Length + 1 + palabra.Length > maximo)
                {
                    yield return actual.ToString();
                    actual.Clear();
                }

                if (actual.Length > 0)
                {
                    actual.Append(' ');
                }

                // Una "palabra" absurda (ej. una tira sin espacios) también se corta.
                actual.Append(palabra.Length > maximo ? palabra[..maximo] : palabra);
            }

            if (actual.Length > 0)
            {
                yield return actual.ToString();
            }
        }
    }

    [GeneratedRegex(@"(\p{L})-\n\s*(\p{Ll})")]
    private static partial Regex PalabraCortada();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex Espacios();

    [GeneratedRegex(@"^(cap[ií]tulo|t[ií]tulo|secci[oó]n|anexo|parte)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EncabezadoConPalabraClave();

    [GeneratedRegex(@"^(art[ií]culo|art\.)\s*\d", RegexOptions.IgnoreCase)]
    private static partial Regex InicioDeArticulo();

    [GeneratedRegex(@"[.:;!?]$")]
    private static partial Regex FinDeOracion();

    [GeneratedRegex(@"(?<=[.!?;])\s+")]
    private static partial Regex Oraciones();

    private sealed record Bloque(string Texto, int Pagina, bool EsEncabezado, bool EmpiezaParrafo);

    /// <summary>Va juntando piezas hasta llenar un fragmento.</summary>
    private sealed class Armado(int maximo, int solapamiento)
    {
        private readonly StringBuilder _texto = new();
        private int _paginaDesde;
        private int _paginaHasta;
        private bool _tieneCuerpo;
        private string? _seccion;
        private bool _encabezadoPendiente;

        public List<FragmentoTexto> Fragmentos { get; } = [];

        public void Encabezado(string encabezado, int pagina)
        {
            // Encabezados seguidos ("TÍTULO II" + "CAPÍTULO 1") se juntan en una sola sección.
            if (_encabezadoPendiente && !_tieneCuerpo)
            {
                _seccion = $"{_seccion} — {encabezado}";
                _texto.Append('\n').Append(encabezado);
                return;
            }

            Cerrar();
            _seccion = encabezado;
            _encabezadoPendiente = true;
            Empezar(encabezado, pagina);
        }

        public void Agregar(string pieza, int pagina, bool empiezaParrafo)
        {
            var separador = empiezaParrafo ? "\n\n" : " ";
            if (_tieneCuerpo && _texto.Length + separador.Length + pieza.Length > maximo)
            {
                var cola = Cola();
                var paginaCola = _paginaHasta;
                Cerrar();
                if (cola.Length > 0)
                {
                    Empezar($"…{cola}", paginaCola);
                }
            }

            if (_texto.Length == 0)
            {
                _paginaDesde = pagina;
            }
            else
            {
                _texto.Append(separador);
            }

            _texto.Append(pieza);
            _paginaHasta = Math.Max(_paginaHasta, pagina);
            _tieneCuerpo = true;
            _encabezadoPendiente = false;
        }

        public void Cerrar()
        {
            if (_tieneCuerpo)
            {
                Fragmentos.Add(new FragmentoTexto(Fragmentos.Count + 1, _texto.ToString().Trim(), _seccion, _paginaDesde, _paginaHasta));
            }

            _texto.Clear();
            _tieneCuerpo = false;
            _encabezadoPendiente = false;
        }

        private void Empezar(string texto, int pagina)
        {
            _texto.Append(texto);
            _paginaDesde = pagina;
            _paginaHasta = pagina;
        }

        /// <summary>Final del fragmento actual, cortado en un límite de palabra, para repetirlo al principio del siguiente.</summary>
        private string Cola()
        {
            if (solapamiento == 0 || _texto.Length <= solapamiento)
            {
                return string.Empty;
            }

            var cola = _texto.ToString(_texto.Length - solapamiento, solapamiento);
            var espacio = cola.IndexOfAny([' ', '\n']);
            return (espacio >= 0 ? cola[(espacio + 1)..] : cola).Replace('\n', ' ').Trim();
        }
    }
}
