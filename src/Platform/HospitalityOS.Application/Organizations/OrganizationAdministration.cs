using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Organizations;

internal sealed class OrganizationAdministration(
    IOrganizationRepository organizations,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IOrganizationAdministration
{
    public async Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        OrganizationId id = new(idGenerators.Resolve().NewId());
        Result<Organization> created = Organization.Create(id, currentTenant.TenantId, request.Name!, timeProvider.GetUtcNow());

        return await commit.CreateAsync(
            AdministrationActions.OrganizationCreate,
            created,
            organization => organizations.AddAsync(organization, cancellationToken),
            organization => Target(organization.Id),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<OrganizationResponse>> RenameAsync(OrganizationId id, RenameOrganizationRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Organization? organization = await organizations.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            AdministrationActions.OrganizationRename,
            Target(id),
            organization,
            AdministrationErrors.OrganizationNotFound,
            expectedVersion,
            found => Task.FromResult(found.Rename(request.Name!, timeProvider.GetUtcNow())),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<OrganizationResponse>> GetAsync(OrganizationId id, CancellationToken cancellationToken)
    {
        Organization? organization = await organizations.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return organization is null ? AdministrationErrors.OrganizationNotFound : Response(organization);
    }

    public async Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Organization> all = await organizations.ListAsync(cancellationToken).ConfigureAwait(false);

        return [.. all.OrderBy(organization => organization.Name, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    private static AuditTarget Target(OrganizationId id) => new(nameof(Organization), id.ToString());

    private static OrganizationResponse Response(Organization organization) => new(organization.Id, organization.Name, organization.Version);
}
