using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationTests.Capabilities.TestCapabilities;

internal sealed record NullOutputInput(string Note);

internal sealed record NullOutputOutput(string Required);

/// <summary>
/// A defective capability whose output breaks its own schema (a null where none is allowed).
/// </summary>
internal sealed class NullOutputCapability : ICapability<NullOutputInput, NullOutputOutput>
{
    public const string Name = "Diagnostics.BrokenOutput";

    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = Name,
        Version = 1,
        Description = "Returns output that breaks its schema.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.System),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.None,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };

    // The null-forgiving operator is the defect under test: the schema says Required is never null.
    public Task<Result<NullOutputOutput>> ExecuteAsync(NullOutputInput input, CancellationToken cancellationToken) =>
        Task.FromResult<Result<NullOutputOutput>>(new NullOutputOutput(null!));
}
