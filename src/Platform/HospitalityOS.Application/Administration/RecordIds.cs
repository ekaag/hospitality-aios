using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;

namespace HospitalityOS.Application.Administration;

/// <summary>
/// The IDs new records get: new ones from the bound <see cref="IIdGenerator"/>, unless a caller that needs known IDs (the
/// reference data seeder) has reserved some. Reserved IDs are taken in order: each new record's own ID first, then the
/// IDs of what it contains in request order (a menu, then its items). Scoped.
/// </summary>
internal sealed class RecordIds(IBindingResolver<IIdGenerator> idGenerators)
{
    private readonly Queue<Guid> reserved = new();

    /// <summary>
    /// How many reserved IDs no record has taken yet.
    /// </summary>
    public int Unused => reserved.Count;

    public Guid Next() => reserved.TryDequeue(out Guid id) ? id : idGenerators.Resolve().NewId();

    /// <summary>
    /// Makes the next records created in this scope take <paramref name="ids"/>.
    /// </summary>
    public void Reserve(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        foreach (Guid id in ids)
        {
            reserved.Enqueue(id);
        }
    }

    /// <summary>
    /// Drops reserved IDs a rejected command did not take, so they are not given to unrelated records.
    /// </summary>
    public void Clear() => reserved.Clear();
}
