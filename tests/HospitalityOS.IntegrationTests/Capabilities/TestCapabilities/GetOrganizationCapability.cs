using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationTests.Capabilities.TestCapabilities;

internal sealed record GetOrganizationInput(OrganizationId OrganizationId);

internal sealed record GetOrganizationOutput(string Name, long Version);

internal sealed class GetOrganizationCapability(IOrganizationRepository organizations, CapabilityProbe probe)
    : ICapability<GetOrganizationInput, GetOrganizationOutput>
{
    public const string Name = "Organization.Get";

    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = Name,
        Version = 1,
        Description = "Reads an organization.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.System, CapabilityCaller.Agent, CapabilityCaller.Guest, CapabilityCaller.Worker),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.None,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<GetOrganizationOutput>> ExecuteAsync(GetOrganizationInput input, CancellationToken cancellationToken)
    {
        probe.Ran(Name);
        Organization? organization = await organizations.GetByIdAsync(input.OrganizationId, cancellationToken);

        return organization is null
            ? Error.NotFound("organization.not_found", "No such organization.")
            : new GetOrganizationOutput(organization.Name, organization.Version);
    }
}
