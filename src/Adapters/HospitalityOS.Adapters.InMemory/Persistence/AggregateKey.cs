namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// Identifies one stored aggregate: IDs are unique per aggregate type.
/// </summary>
internal readonly record struct AggregateKey(Type AggregateType, Guid Id);
