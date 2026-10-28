using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using ContractSubject = HospitalityOS.Contracts.Api.Properties.SubjectReference;

namespace HospitalityOS.Application.Administration;

/// <summary>
/// Resolves what an administrative request refers to: the property a new record is for, a space, or the record that
/// hours, a policy, or content are about. Repositories filter by the caller's tenant, so another tenant's records are
/// missing; records of another property count as missing too. Scoped.
/// </summary>
internal sealed class PropertyReferences(
    IPropertyRepository properties,
    ISpaceRepository spaces,
    IAmenityRepository amenities,
    IRestaurantRepository restaurants,
    IServiceRepository services)
{
    /// <summary>
    /// The property a new record is for, which must exist in the caller's tenant.
    /// </summary>
    /// <returns>The property's ID, or a validation error on <c>propertyId</c>: <c>{codePrefix}.property_required</c> or
    /// <c>{codePrefix}.property_not_found</c>.</returns>
    public async Task<Result<PropertyId>> PropertyAsync(PropertyId? id, string codePrefix, CancellationToken cancellationToken)
    {
        if (id is not { } propertyId)
        {
            return Error.Validation("propertyId", codePrefix + ".property_required", "propertyId is required.");
        }

        Property? property = await properties.GetByIdAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return property is null ? Error.Validation("propertyId", codePrefix + ".property_not_found", "There is no such property.") : propertyId;
    }

    /// <summary>
    /// The space a request names, or <c>null</c> when it names none. A missing space is recorded as <paramref name="code"/>;
    /// a space of another property is returned, for the domain to refuse with its own error.
    /// </summary>
    public async Task<Space?> SpaceAsync(SpaceId? id, string field, string code, RequestFields fields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (id is not { } spaceId)
        {
            return null;
        }

        Space? space = await spaces.GetByIdAsync(spaceId, cancellationToken).ConfigureAwait(false);

        if (space is null)
        {
            fields.Add(field, code, "There is no such space.");
        }

        return space;
    }

    /// <summary>
    /// The location a request names for a record that holds only a space's ID (restaurants, events), so the domain cannot
    /// check it: a space missing or in another property is recorded as <paramref name="code"/>.
    /// </summary>
    public async Task<SpaceId?> LocationAsync(PropertyId propertyId, SpaceId? id, string field, string code, RequestFields fields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (id is not { } spaceId)
        {
            return null;
        }

        if (!InProperty(await spaces.GetByIdAsync(spaceId, cancellationToken).ConfigureAwait(false), propertyId))
        {
            fields.Add(field, code, "There is no such space in this property.");
        }

        return spaceId;
    }

    /// <summary>
    /// The subject a request names, or <c>null</c> when it names none. A missing kind is recorded as
    /// <c>{codePrefix}_kind_required</c> and a subject that is not in the property as <c>{codePrefix}_not_found</c>; a
    /// missing ID is left for the domain to report.
    /// </summary>
    public async Task<SubjectReference?> SubjectAsync(
        PropertyId propertyId,
        ContractSubject? subject,
        string field,
        string codePrefix,
        RequestFields fields,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (subject is null)
        {
            return null;
        }

        SubjectReference reference = new(fields.Required<SubjectKind>(subject.Kind, field + ".kind", codePrefix + "_kind_required"), subject.Id ?? Guid.Empty);

        if (subject.Kind is not null && reference.Id != Guid.Empty && !await IsInPropertyAsync(propertyId, reference, cancellationToken).ConfigureAwait(false))
        {
            fields.Add(field + ".id", codePrefix + "_not_found", $"There is no such {subject.Kind} in this property.");
        }

        return reference;
    }

    private async Task<bool> IsInPropertyAsync(PropertyId propertyId, SubjectReference subject, CancellationToken cancellationToken) =>
        subject.Kind switch
        {
            SubjectKind.Property => subject.Id == propertyId.Value,
            SubjectKind.Space => InProperty(await spaces.GetByIdAsync(new SpaceId(subject.Id), cancellationToken).ConfigureAwait(false), propertyId),
            SubjectKind.Amenity => InProperty(await amenities.GetByIdAsync(new AmenityId(subject.Id), cancellationToken).ConfigureAwait(false), propertyId),
            SubjectKind.Restaurant => InProperty(await restaurants.GetByIdAsync(new RestaurantId(subject.Id), cancellationToken).ConfigureAwait(false), propertyId),
            SubjectKind.Service => InProperty(await services.GetByIdAsync(new ServiceId(subject.Id), cancellationToken).ConfigureAwait(false), propertyId),
            _ => false,
        };

    private static bool InProperty(IPropertyScoped? record, PropertyId propertyId) => record?.PropertyId == propertyId;
}
