using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using DomainAddress = HospitalityOS.Domain.Properties.Properties.PostalAddress;

namespace HospitalityOS.Application.Properties;

internal sealed class PropertyAdministration(
    IPropertyRepository properties,
    IOrganizationRepository organizations,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IPropertyAdministration
{
    public async Task<Result<PropertyResponse>> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Organization? organization = request.OrganizationId is { } organizationId
            ? await organizations.GetByIdAsync(organizationId, cancellationToken).ConfigureAwait(false)
            : null;

        if (organization is null)
        {
            Error missing = request.OrganizationId is null
                ? Error.Validation("organizationId", "property.organization_required", "organizationId is required.")
                : Error.Validation("organizationId", "property.organization_not_found", "There is no such organization.");

            return await commit.RejectAsync(AdministrationActions.PropertyCreate, missing, null, cancellationToken).ConfigureAwait(false);
        }

        PropertyId id = new(idGenerators.Resolve().NewId());
        Result<Property> created = Property.Create(id, currentTenant.TenantId, organization.Id, Details(request.Name, request.TimeZone, request.Address, request.ContactPhone, request.ContactEmail), timeProvider.GetUtcNow());

        if (created.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.PropertyCreate, created.Error, null, cancellationToken).ConfigureAwait(false);
        }

        await properties.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        Result committed = await commit.SucceedAsync(AdministrationActions.PropertyCreate, Target(id), cancellationToken).ConfigureAwait(false);

        return committed.IsFailure ? committed.Error : Response(created.Value);
    }

    public async Task<Result<PropertyResponse>> UpdateAsync(PropertyId id, UpdatePropertyRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Property? property = await properties.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        Result updated = property is null ? AdministrationErrors.PropertyNotFound
            : expectedVersion is { } expected && expected != property.Version ? AdministrationErrors.VersionMismatch(expected, property.Version)
            : property.Update(Details(request.Name, request.TimeZone, request.Address, request.ContactPhone, request.ContactEmail), timeProvider.GetUtcNow());

        if (updated.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.PropertyUpdate, updated.Error, Target(id), cancellationToken).ConfigureAwait(false);
        }

        Result committed = await commit.SucceedAsync(AdministrationActions.PropertyUpdate, Target(id), cancellationToken).ConfigureAwait(false);

        return committed.IsFailure ? committed.Error : Response(property!);
    }

    public async Task<Result<PropertyResponse>> GetAsync(PropertyId id, CancellationToken cancellationToken)
    {
        Property? property = await properties.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return property is null ? AdministrationErrors.PropertyNotFound : Response(property);
    }

    public async Task<IReadOnlyList<PropertyResponse>> ListAsync(OrganizationId? organizationId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Property> found = await properties.ListAsync(organizationId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(property => property.Details.Name, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    private static AuditTarget Target(PropertyId id) => new(nameof(Property), id.ToString(), id);

    // The domain validates every field, including missing ones, so absent values pass through as null.
    private static PropertyDetails Details(string? name, string? timeZone, PropertyAddress? address, string? phone, string? email) =>
        new(
            name!,
            timeZone!,
            address is null ? null! : new DomainAddress(address.Line1!, address.Line2, address.City!, address.Region, address.PostalCode, address.CountryCode!),
            phone,
            email);

    private static PropertyResponse Response(Property property)
    {
        PropertyDetails details = property.Details;
        DomainAddress address = details.Address;

        return new PropertyResponse(
            property.Id,
            property.OrganizationId,
            details.Name,
            details.TimeZoneId,
            new PropertyAddress(address.Line1, address.Line2, address.City, address.Region, address.PostalCode, address.CountryCode),
            details.ContactPhone,
            details.ContactEmail,
            property.Version);
    }
}
