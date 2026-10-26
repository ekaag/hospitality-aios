using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// Hidden (shadow) columns holding normalized lookup keys that the domain keeps inside value objects or compares
/// case-insensitively. EF Core cannot index columns of a complex type, so these copies carry the unique indexes
/// (P8) and the repositories' lookups. They are recomputed from the aggregate on every save.
/// </summary>
internal static class KeyColumns
{
    public const string UnitNumberKey = "UnitNumberKey";
    public const string ServiceCodeKey = "CodeKey";
    public const string HoursSubjectKey = "SubjectKey";
    public const string SourceMessageId = "SourceMessageId";
    public const string SucceededIdempotencyKey = "SucceededIdempotencyKey";

    private static readonly Dictionary<Type, (string Column, Func<object, object?> Value)[]> byType = new()
    {
        [typeof(AccommodationUnit)] = [(UnitNumberKey, unit => UnitNumber(((AccommodationUnit)unit).UnitNumber))],
        [typeof(Service)] = [(ServiceCodeKey, service => ServiceCode(((Service)service).Details.Code))],
        [typeof(OperatingHours)] = [(HoursSubjectKey, hours => Subject(((OperatingHours)hours).Subject))],
        [typeof(ServiceRequest)] = [(SourceMessageId, request => ((ServiceRequest)request).Origin.SourceMessageId?.Value)],
        [typeof(CapabilityInvocation)] = [(SucceededIdempotencyKey, invocation => SucceededIdempotency((CapabilityInvocation)invocation))],
    };

    public static string UnitNumber(string unitNumber) => unitNumber.Trim().ToUpperInvariant();

    public static string ServiceCode(string code) => code.Trim().ToLowerInvariant();

    public static string Subject(SubjectReference subject) => $"{subject.Kind}:{subject.Id}";

    /// <summary>
    /// A capability name cannot contain a colon, so the pair is unambiguous.
    /// </summary>
    public static string SucceededIdempotency(string capabilityName, string idempotencyKey) => $"{capabilityName}:{idempotencyKey}";

    /// <summary>
    /// Set only on succeeded invocations with a key, so a failed attempt never blocks a retry with the same key.
    /// </summary>
    private static string? SucceededIdempotency(CapabilityInvocation invocation) =>
        invocation is { Status: CapabilityInvocationStatus.Succeeded, IdempotencyKey: { } key } ? SucceededIdempotency(invocation.CapabilityName, key) : null;

    /// <summary>
    /// The key's name as conflict errors report it, e.g. <c>unitNumber</c> for <see cref="UnitNumberKey"/>.
    /// </summary>
    public static string Describe(string column)
    {
        string name = column.EndsWith("Key", StringComparison.Ordinal) ? column[..^3] : column;

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static void Apply(ChangeTracker changeTracker)
    {
        foreach (EntityEntry entry in changeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified) || !byType.TryGetValue(entry.Metadata.ClrType, out (string Column, Func<object, object?> Value)[]? keys))
            {
                continue;
            }

            foreach ((string column, Func<object, object?> value) in keys)
            {
                entry.Property(column).CurrentValue = value(entry.Entity);
            }
        }
    }
}
