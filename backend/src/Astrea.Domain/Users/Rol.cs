using Astrea.Domain.Common;

namespace Astrea.Domain.Users;

/// <summary>
/// Catálogo fijo de roles del sistema. Los identificadores son constantes para poder sembrarlos
/// en la base de datos y referenciarlos sin consultas adicionales.
/// </summary>
public sealed class Rol : Entity
{
    public const string CodigoEstudiante = "ESTUDIANTE";
    public const string CodigoAdministrador = "ADMINISTRADOR";

    public static readonly Rol Estudiante = new(
        new Guid("0a6a8f1e-4c5b-4d1e-9b77-2f6c1b9d0e01"),
        CodigoEstudiante,
        "Estudiante",
        "Consulta información académica, inicia trámites y publica hallazgos.");

    public static readonly Rol Administrador = new(
        new Guid("0a6a8f1e-4c5b-4d1e-9b77-2f6c1b9d0e02"),
        CodigoAdministrador,
        "Administrador",
        "Personal de Secretaría: administra planes, mesas, trámites, hallazgos y documentos.");

    public static IReadOnlyList<Rol> Catalogo { get; } = [Estudiante, Administrador];

    private Rol()
    {
    }

    private Rol(Guid id, string codigo, string nombre, string descripcion)
        : base(id)
    {
        Codigo = codigo;
        Nombre = nombre;
        Descripcion = descripcion;
    }

    public string Codigo { get; private set; } = null!;

    public string Nombre { get; private set; } = null!;

    public string Descripcion { get; private set; } = null!;

    public static Rol? PorId(Guid id) => Catalogo.FirstOrDefault(rol => rol.Id == id);
}
