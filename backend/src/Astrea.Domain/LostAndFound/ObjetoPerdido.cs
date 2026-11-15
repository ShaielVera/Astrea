using Astrea.Domain.Common;

namespace Astrea.Domain.LostAndFound;

public enum CategoriaObjeto
{
    Electronica = 1,
    Documentacion = 2,
    Llaves = 3,
    Ropa = 4,
    Utiles = 5,
    TermoOBotella = 6,
    Accesorios = 7,
    Herramientas = 8,
    Otro = 9,
}

/// <summary>Lugar del instituto donde quedó guardado el objeto, a donde va el dueño a buscarlo.</summary>
public enum PuntoResguardo
{
    // Se guardan por nombre (no por número), así que el orden se puede cambiar sin migrar datos.
    Direccion = 1,
    Secretaria = 2,
    Biblioteca = 3,
    AulaInfraestructura = 4,

    /// <summary>Para herramientas, equipos u objetos grandes.</summary>
    Deposito = 5,
}

public enum FranjaHoraria
{
    Manana = 1,
    Tarde = 2,
    Noche = 3,
}

/// <summary>Publicado (visible en la búsqueda) → Entregado a su dueño, o Dado de baja por la Secretaría. Ambos son finales.</summary>
public enum EstadoObjeto
{
    Publicado = 1,
    Entregado = 2,
    DadoDeBaja = 3,
}

/// <summary>
/// Objeto encontrado en el instituto. Separa lo público (título, categoría, lugar, franja, punto de resguardo, foto)
/// de lo privado: la <see cref="DescripcionPrivada"/> tiene los detalles que solo conoce el dueño y sirve para
/// comprobar en persona que quien lo reclama es realmente el dueño.
/// </summary>
public sealed class ObjetoPerdido : Entity
{
    /// <summary>Un hallazgo de hace más tiempo ya no se publica: probablemente el objeto ya no esté.</summary>
    public const int AntiguedadMaximaDias = 60;

    /// <summary>Días que se conserva un hallazgo cerrado (entregado o dado de baja) antes de borrarlo con su foto y los datos de quien lo retiró.</summary>
    public const int DiasDeConservacion = 30;

    private ObjetoPerdido()
    {
    }

    private ObjetoPerdido(Guid id, Guid publicadoPorUsuarioId, DateTimeOffset ahora)
        : base(id)
    {
        PublicadoPorUsuarioId = publicadoPorUsuarioId;
        Estado = EstadoObjeto.Publicado;
        FechaPublicacion = ahora;
    }

    public Guid PublicadoPorUsuarioId { get; private set; }

    public string TituloPublico { get; private set; } = null!;

    /// <summary>Solo la ven la Secretaría y quien lo publicó. Nunca sale en la búsqueda.</summary>
    public string DescripcionPrivada { get; private set; } = null!;

    public CategoriaObjeto Categoria { get; private set; }

    /// <summary>Dónde se encontró, aproximado (ej. "Aula 3, planta baja").</summary>
    public string SectorEncontrado { get; private set; } = null!;

    public FranjaHoraria FranjaHoraria { get; private set; }

    public PuntoResguardo PuntoResguardo { get; private set; }

    public DateOnly FechaHallazgo { get; private set; }

    public string? FotoStoragePath { get; private set; }

    public string? FotoContentType { get; private set; }

    public EstadoObjeto Estado { get; private set; }

    public DateTimeOffset FechaPublicacion { get; private set; }

    public DateTimeOffset? FechaCierre { get; private set; }

    public string? MotivoBaja { get; private set; }

    public Guid? DadoDeBajaPorUsuarioId { get; private set; }

    public EntregaObjeto? Entrega { get; private set; }

    public bool EstaPublicado => Estado == EstadoObjeto.Publicado;

    /// <summary>Solo aparece en la búsqueda con foto: es lo que le permite al dueño reconocerlo (ej. dos cartucheras parecidas).</summary>
    public bool EsVisibleEnBusqueda => EstaPublicado && FotoStoragePath is not null;

    /// <summary>Cerrado hace <see cref="DiasDeConservacion"/> días o más: ya no hace falta conservarlo.</summary>
    public bool DebeDepurarse(DateTimeOffset ahora) => FechaCierre is { } cierre && cierre.AddDays(DiasDeConservacion) <= ahora;

    public static ObjetoPerdido Publicar(Guid id, Guid publicadoPorUsuarioId, DatosHallazgo datos, DateOnly hoy, DateTimeOffset ahora)
    {
        if (publicadoPorUsuarioId == Guid.Empty)
        {
            throw new DomainException("El hallazgo debe registrar quién lo publicó.");
        }

        if (datos.FechaHallazgo > hoy)
        {
            throw new DomainException("La fecha del hallazgo no puede ser futura.");
        }

        if (datos.FechaHallazgo < hoy.AddDays(-AntiguedadMaximaDias))
        {
            throw new DomainException($"Solo se publican hallazgos de los últimos {AntiguedadMaximaDias} días.");
        }

        return new ObjetoPerdido(id, publicadoPorUsuarioId, ahora)
        {
            TituloPublico = Texto.Requerido(datos.TituloPublico, "título", 80),
            DescripcionPrivada = Texto.Requerido(datos.DescripcionPrivada, "texto de la descripción privada", 500),
            Categoria = Validar(datos.Categoria, "La categoría no es válida."),
            SectorEncontrado = Texto.Requerido(datos.SectorEncontrado, "lugar donde se encontró", 80),
            FranjaHoraria = Validar(datos.FranjaHoraria, "La franja horaria no es válida."),
            PuntoResguardo = Validar(datos.PuntoResguardo, "El punto de resguardo no es válido."),
            FechaHallazgo = datos.FechaHallazgo,
        };
    }

    public bool PuedeEditar(Guid usuarioId, bool esAdministrador) => esAdministrador || usuarioId == PublicadoPorUsuarioId;

    /// <summary>Asigna (o reemplaza) la foto. Devuelve la ruta anterior, para borrarla del almacenamiento.</summary>
    public string? AsignarFoto(string storagePath, string contentType)
    {
        ValidarPublicado();
        var anterior = FotoStoragePath;
        FotoStoragePath = Texto.Requerido(storagePath, "ruta de la foto", 300);
        FotoContentType = contentType;
        return anterior;
    }

    /// <summary>
    /// Entrega el objeto a quien lo reclama. Exige que el administrador confirme de forma explícita
    /// que verificó en persona su identidad (y los detalles privados): el booleano queda registrado con su autor y fecha.
    /// </summary>
    public EntregaObjeto Entregar(Guid id, Guid administradorId, string nombreRetirante, string dniRetirante, bool identidadVerificada, string? observacionInterna, DateTimeOffset ahora)
    {
        ValidarPublicado();
        if (!identidadVerificada)
        {
            throw new DomainException("Para entregar el objeto confirmá que verificaste en persona la identidad de quien lo retira.");
        }

        Entrega = new EntregaObjeto(id, Id, nombreRetirante, dniRetirante, administradorId, observacionInterna, ahora);
        Estado = EstadoObjeto.Entregado;
        FechaCierre = ahora;
        return Entrega;
    }

    /// <summary>La Secretaría lo retira de la búsqueda (publicación inapropiada, duplicada o error). El motivo queda registrado.</summary>
    public void DarDeBaja(Guid administradorId, string motivo, DateTimeOffset ahora)
    {
        ValidarPublicado();
        MotivoBaja = Texto.Requerido(motivo, "motivo de la baja", 300);
        DadoDeBajaPorUsuarioId = administradorId;
        Estado = EstadoObjeto.DadoDeBaja;
        FechaCierre = ahora;
    }

    private void ValidarPublicado()
    {
        if (!EstaPublicado)
        {
            throw new DomainException(Estado == EstadoObjeto.Entregado
                ? "El objeto ya fue entregado."
                : "El hallazgo fue dado de baja.");
        }
    }

    private static T Validar<T>(T valor, string mensaje)
        where T : struct, Enum =>
        Enum.IsDefined(valor) ? valor : throw new DomainException(mensaje);
}

/// <summary>Datos que completa quien publica el hallazgo.</summary>
public sealed record DatosHallazgo(
    string TituloPublico,
    string DescripcionPrivada,
    CategoriaObjeto Categoria,
    string SectorEncontrado,
    FranjaHoraria FranjaHoraria,
    PuntoResguardo PuntoResguardo,
    DateOnly FechaHallazgo);

/// <summary>
/// Registro de la entrega: quién lo retiró (identidad declarada y DNI), quién verificó y cuándo.
/// Solo lo ve el personal autorizado.
/// </summary>
public sealed class EntregaObjeto : Entity
{
    private EntregaObjeto()
    {
    }

    internal EntregaObjeto(Guid id, Guid objetoPerdidoId, string nombreRetirante, string dniRetirante, Guid verificadoPorUsuarioId, string? observacionInterna, DateTimeOffset fechaEntrega)
        : base(id)
    {
        var dni = new string((dniRetirante ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (dni.Length is < 7 or > 9 || (dniRetirante ?? string.Empty).Any(c => !char.IsAsciiDigit(c) && c is not ('.' or ' ' or '-')))
        {
            throw new DomainException("El DNI de quien retira debe tener entre 7 y 9 dígitos.");
        }

        if (verificadoPorUsuarioId == Guid.Empty)
        {
            throw new DomainException("La entrega debe registrar quién la verificó.");
        }

        ObjetoPerdidoId = objetoPerdidoId;
        NombreRetirante = Texto.Requerido(nombreRetirante, "nombre de quien retira", 120);
        DniRetirante = dni;
        VerificadoPorUsuarioId = verificadoPorUsuarioId;
        IdentidadVerificada = true;
        ObservacionInterna = Texto.Opcional(observacionInterna, "texto de la observación", 500);
        FechaEntrega = fechaEntrega;
    }

    public Guid ObjetoPerdidoId { get; private set; }

    public string NombreRetirante { get; private set; } = null!;

    public string DniRetirante { get; private set; } = null!;

    public Guid VerificadoPorUsuarioId { get; private set; }

    public bool IdentidadVerificada { get; private set; }

    public string? ObservacionInterna { get; private set; }

    public DateTimeOffset FechaEntrega { get; private set; }
}
