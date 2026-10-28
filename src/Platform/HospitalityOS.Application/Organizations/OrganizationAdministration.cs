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

        if (created.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.OrganizationCreate, created.Error, null, cancellationToken).ConfigureAwait(false);
        }

        await organizations.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        Result committed = await commit.SucceedAsync(AdministrationActions.OrganizationCreate, Target(id), cancellationToken).ConfigureAwait(false);

        return committed.IsFailure ? committed.Error : Response(created.Value);
    }

    public async Task<Result<OrganizationResponse>> RenameAsync(OrganizationId id, RenameOrganizationRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Organization? organization = await organizations.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        Result renamed = organization is null ? AdministrationErrors.OrganizationNotFound
            : expectedVersion is { } expected && expected != organization.Version ? AdministrationErrors.VersionMismatch(expected, organization.Version)
            : organization.Rename(request.Name!, timeProvider.GetUtcNow());

        if (renamed.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.OrganizationRename, renamed.Error, Target(id), cancellationToken).ConfigureAwait(false);
        }

        Result committed = await commit.SucceedAsync(AdministrationActions.OrganizationRename, Target(id), cancellationToken).ConfigureAwait(false);

        return committed.IsFailure ? committed.Error : Response(organization!);
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
