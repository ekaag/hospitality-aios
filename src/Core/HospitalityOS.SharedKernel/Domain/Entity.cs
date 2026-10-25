namespace HospitalityOS.SharedKernel.Domain;

/// <summary>
/// An object defined by its identity: two entities of the same type with the same ID are equal.
/// </summary>
/// <typeparam name="TId">The strongly typed ID.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct, IEquatable<TId>
{
    /// <summary>
    /// Creates an entity with the given identity.
    /// </summary>
    /// <param name="id">The identity; must not be the default value.</param>
    protected Entity(TId id)
    {
        if (id.Equals(default))
        {
            throw new ArgumentException($"{GetType().Name} requires a non-default {typeof(TId).Name}.", nameof(id));
        }

        Id = id;
    }

    /// <summary>
    /// The entity's identity.
    /// </summary>
    public TId Id { get; }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other) || (other.GetType() == GetType() && Id.Equals(other.Id));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
