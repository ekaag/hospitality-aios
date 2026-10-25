using HospitalityOS.Ports.Ids;

namespace HospitalityOS.Adapters.Local.IdGenerator;

/// <summary>
/// Generates time-ordered GUID v7 identifiers, which keep database indexes compact.
/// </summary>
internal sealed class GuidV7IdGenerator : IIdGenerator
{
    private readonly TimeProvider timeProvider;

    public GuidV7IdGenerator(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public Guid NewId() => Guid.CreateVersion7(timeProvider.GetUtcNow());
}
