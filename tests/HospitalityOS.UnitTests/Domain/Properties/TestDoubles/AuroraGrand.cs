using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.UnitTests.Domain.Properties.TestDoubles;

/// <summary>
/// Fixed IDs and builders for the Aurora Grand reference property, so tests read like the spec's examples.
/// </summary>
internal static class AuroraGrand
{
    public static TenantId TenantId { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900001"));

    public static PropertyId PropertyId { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900002"));

    public static PropertyId OtherPropertyId { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209900003"));

    public static DateTimeOffset Now { get; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static Space Space(string name, SpaceKind kind, Space? parent = null, PropertyId? propertyId = null, int idSuffix = 100)
    {
        SpaceId id = new(Guid.Parse($"01890a5d-ac96-774b-bcce-b302099{idSuffix:D5}"));
        Space space = HospitalityOS.Domain.Properties.Spaces.Space
            .Create(id, TenantId, propertyId ?? PropertyId, parent, new SpaceDetails(name, kind), Now)
            .Value;
        space.DequeueDomainEvents();

        return space;
    }

    public static AccommodationType DeluxeKing(PropertyId? propertyId = null, int idSuffix = 200)
    {
        AccommodationTypeId id = new(Guid.Parse($"01890a5d-ac96-774b-bcce-b302099{idSuffix:D5}"));
        AccommodationType type = AccommodationType
            .Create(id, TenantId, propertyId ?? PropertyId, new AccommodationTypeDetails("Deluxe King", 2), Now)
            .Value;
        type.DequeueDomainEvents();

        return type;
    }
}
