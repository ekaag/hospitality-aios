using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationTests.Capabilities.TestCapabilities;

internal sealed record RenameOrganizationInput(OrganizationId OrganizationId, string Name, string? FailWith = null);

internal sealed record RenameOrganizationOutput(OrganizationId OrganizationId, long Version);

/// <summary>
/// Renames an organization. <see cref="RenameOrganizationInput.FailWith"/> makes it fail after renaming (returning that
/// error code, or throwing for <c>crash</c>), to prove a failed attempt's changes are discarded.
/// </summary>
internal sealed class RenameOrganizationCapability(IOrganizationRepository organizations, TimeProvider timeProvider, CapabilityProbe probe)
    : ICapability<RenameOrganizationInput, RenameOrganizationOutput>
{
    public const string Name = "Organization.Rename";

    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = Name,
        Version = 1,
        Description = "Renames an organization.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Admin, CapabilityCaller.Agent),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.KeyRequired,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<RenameOrganizationOutput>> ExecuteAsync(RenameOrganizationInput input, CancellationToken cancellationToken)
    {
        probe.Ran(Name);
        Organization? organization = await organizations.GetByIdAsync(input.OrganizationId, cancellationToken);

        // Racers wait here, after loading, so both change the version they loaded and one must lose at commit.
        await probe.ArriveAsync(cancellationToken);

        if (organization is null)
        {
            return Error.NotFound("organization.not_found", "No such organization.");
        }

        Result renamed = organization.Rename(input.Name, timeProvider.GetUtcNow());

        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        return input.FailWith switch
        {
            null => new RenameOrganizationOutput(organization.Id, organization.Version),
            "crash" => throw new InvalidOperationException("The capability crashed."),
            string code => Error.InvalidState(code, "Failed on purpose after renaming."),
        };
    }
}
