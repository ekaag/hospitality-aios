using System.Text.Json;
using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <summary>
/// An aggregate's public state as JSON, so a test can check that what was loaded equals what was saved
/// (aggregates compare by ID only).
/// </summary>
internal static class AggregateState
{
    public static string Of(IAggregateRoot aggregate) => JsonSerializer.Serialize(aggregate, aggregate.GetType());
}
