using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Organizations;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Eventing.Handling;

namespace HospitalityOS.IntegrationTests.Eventing;

/// <summary>
/// Appends "!" to a new organization's name, so running twice would visibly leave "!!".
/// </summary>
internal sealed class ExclaimHandler(IOrganizationRepository organizations, HandlerProbe probe, TimeProvider timeProvider) : IEventHandler<OrganizationCreatedV1>
{
    public const string Name = "Test.ExclaimOnOrganizationCreated";

    public async Task HandleAsync(EventEnvelope envelope, OrganizationCreatedV1 payload, CancellationToken cancellationToken)
    {
        await probe.BeforeEffectAsync(payload.OrganizationId.Value, cancellationToken);

        Organization organization = await organizations.GetByIdAsync(payload.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException($"Organization {payload.OrganizationId} not found.");
        organization.Rename(organization.Name + "!", timeProvider.GetUtcNow());
    }
}
