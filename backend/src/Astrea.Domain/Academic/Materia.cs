using Astrea.Domain.Common;

namespace Astrea.Domain.Academic;

/// <summary>
/// Asignatura. Su ubicación (año, cuatrimestre) depende del plan y se guarda en <see cref="PlanMateria"/>.
/// </summary>
public sealed class Materia : Entity
{
    private Materia()
    {
    }

    private Materia(Guid id, string codigo, string nombre)
        : base(id)
    {
        Codigo = codigo;
        Nombre = nombre;
        Activa = true;
    }

    public string Codigo { get; private set; } = null!;

    public string Nombre { get; private set; } = null!;

    public bool Activa { get; private set; }

    public static Materia Crear(Guid id, string codigo, string nombre) =>
        new(id, Texto.Requerido(codigo, "código de la materia", 30).ToUpperInvariant(), Texto.Requerido(nombre, "nombre de la materia", 200));

    public void Actualizar(string nombre, bool activa)
    {
        Nombre = Texto.Requerido(nombre, "nombre de la materia", 200);
        Activa = activa;
    }
}
