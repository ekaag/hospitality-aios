using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Capabilities.Reading;

/// <summary>
/// Cites the record an answer comes from (P4): its type, ID, and the version that was read.
/// </summary>
internal static class Sources
{
    public static SourceReference Of<TId>(AggregateRoot<TId> record)
        where TId : struct, IEquatable<TId>, ITypedId =>
        new(record.GetType().Name, record.Id.Value, record.Version);
}
