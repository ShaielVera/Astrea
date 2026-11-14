namespace Astrea.Domain.Common;

/// <summary>
/// Base de todas las entidades del dominio. La identidad se define por <see cref="Id"/>.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador de la entidad no puede estar vacío.");
        }

        Id = id;
    }

    /// <summary>Constructor para materialización desde la base de datos (EF Core).</summary>
    protected Entity()
    {
    }

    public Guid Id { get; private set; }

    public bool Equals(Entity? other) =>
        other is not null && other.GetType() == GetType() && other.Id == Id;

    public override bool Equals(object? obj) => Equals(obj as Entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
