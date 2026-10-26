using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// A unique index: no two aggregates of <see cref="AggregateType"/> may have equal non-null values.
/// </summary>
/// <param name="AggregateType">The aggregate type the index covers.</param>
/// <param name="Name">The key's name in conflict errors, e.g. <c>unitNumber</c>.</param>
/// <param name="Select">The key value (with value equality), or <c>null</c> when the aggregate has none.</param>
internal sealed record UniqueKey(Type AggregateType, string Name, Func<IAggregateRoot, object?> Select);
