using HospitalityOS.Application.Accommodations;
using HospitalityOS.Application.Amenities;
using HospitalityOS.Application.Content;
using HospitalityOS.Application.Hours;
using HospitalityOS.Application.Organizations;
using HospitalityOS.Application.Policies;
using HospitalityOS.Application.Properties;
using HospitalityOS.Application.Spaces;
using HospitalityOS.Contracts.Api.Accommodations;
using HospitalityOS.Contracts.Api.Amenities;
using HospitalityOS.Contracts.Api.Content;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.Contracts.Api.Organizations;
using HospitalityOS.Contracts.Api.Policies;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using ContentStatus = HospitalityOS.Contracts.Api.Content.ContentStatus;
using PolicyStatus = HospitalityOS.Contracts.Api.Policies.PolicyStatus;

namespace HospitalityOS.Seeding;

/// <summary>
/// The Aurora Grand itself: organization, property, spaces, rooms, amenities and their hours, policies, and content.
/// </summary>
internal sealed class AuroraGrandProperty(
    SeedSteps steps,
    IOrganizationAdministration organizations,
    IPropertyAdministration properties,
    ISpaceAdministration spaces,
    IAccommodationAdministration accommodations,
    IAmenityAdministration amenities,
    IOperatingHoursAdministration hours,
    IPolicyAdministration policies,
    IContentAdministration content)
{
    public async Task SeedAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        await steps.EnsureAsync(
            "organization",
            AuroraGrand.OrganizationId.Value,
            () => organizations.GetAsync(AuroraGrand.OrganizationId, cancellationToken),
            () => organizations.CreateAsync(new CreateOrganizationRequest("Aurora Hotels"), cancellationToken)).ConfigureAwait(false);
        await steps.EnsureAsync(
            "property",
            AuroraGrand.PropertyId.Value,
            () => properties.GetAsync(AuroraGrand.PropertyId, cancellationToken),
            () => properties.CreateAsync(
                new CreatePropertyRequest(
                    AuroraGrand.OrganizationId,
                    "Aurora Grand",
                    AuroraGrand.TimeZone,
                    new PropertyAddress("1 Strand", null, "London", null, "WC2N 5HR", "GB"),
                    "+44 20 7946 0000",
                    "stay@auroragrand.example"),
                cancellationToken)).ConfigureAwait(false);

        await SeedSpacesAsync(cancellationToken).ConfigureAwait(false);
        await SeedRoomsAsync(cancellationToken).ConfigureAwait(false);
        await SeedAmenitiesAsync(localToday, cancellationToken).ConfigureAwait(false);
        await SeedPoliciesAsync(cancellationToken).ConfigureAwait(false);
        await SeedContentAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedSpacesAsync(CancellationToken cancellationToken)
    {
        await SpaceAsync(AuroraGrand.MainBuilding, null, "Main Building", SpaceKind.Building, null, cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.Lobby, AuroraGrand.MainBuilding, "Lobby", SpaceKind.Area, "Through the main entrance on the Strand.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.LobbyBar, AuroraGrand.MainBuilding, "Lobby Bar", SpaceKind.Area, "On the left of the lobby, past reception.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.LeisureLevel, AuroraGrand.MainBuilding, "Leisure Level", SpaceKind.Floor, "Lower ground floor: take the lifts to LG.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.PoolHall, AuroraGrand.LeisureLevel, "Pool Hall", SpaceKind.Area, "Turn right out of the lifts on LG.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.FitnessStudio, AuroraGrand.LeisureLevel, "Fitness Studio", SpaceKind.Area, "Turn left out of the lifts on LG.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.Floor4, AuroraGrand.MainBuilding, "Floor 4", SpaceKind.Floor, null, cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.SaffronDiningRoom, AuroraGrand.MainBuilding, "Saffron Dining Room", SpaceKind.Area, "First floor, up the main staircase.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.HarbourGrillDiningRoom, AuroraGrand.MainBuilding, "Harbour Grill Dining Room", SpaceKind.Area, "Ground floor, off the lobby by the river windows.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.Room412Space, AuroraGrand.Floor4, "Room 412", SpaceKind.Room, null, cancellationToken).ConfigureAwait(false);
        await SpaceAsync(AuroraGrand.Room415Space, AuroraGrand.Floor4, "Room 415", SpaceKind.Room, null, cancellationToken).ConfigureAwait(false);
    }

    private Task<SpaceResponse> SpaceAsync(SpaceId id, SpaceId? parent, string name, SpaceKind kind, string? directions, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"space '{name}'",
            id.Value,
            () => spaces.GetAsync(id, cancellationToken),
            () => spaces.CreateAsync(new CreateSpaceRequest(AuroraGrand.PropertyId, parent, name, kind, Directions: directions), cancellationToken));

    private async Task SeedRoomsAsync(CancellationToken cancellationToken)
    {
        await steps.EnsureAsync(
            "room type Deluxe King",
            AuroraGrand.DeluxeKing,
            () => accommodations.GetTypeAsync(AuroraGrand.DeluxeKing, cancellationToken),
            () => accommodations.CreateTypeAsync(new CreateAccommodationTypeRequest(AuroraGrand.PropertyId, "Deluxe King", 2, "King bed, city view."), cancellationToken)).ConfigureAwait(false);
        await RoomAsync(AuroraGrand.Room412, AuroraGrand.Room412Space, "412", cancellationToken).ConfigureAwait(false);
        await RoomAsync(AuroraGrand.Room415, AuroraGrand.Room415Space, "415", cancellationToken).ConfigureAwait(false);
    }

    private Task<AccommodationUnitResponse> RoomAsync(AccommodationUnitId id, SpaceId space, string number, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"room {number}",
            id.Value,
            () => accommodations.GetUnitAsync(id, cancellationToken),
            () => accommodations.CreateUnitAsync(new CreateAccommodationUnitRequest(AuroraGrand.DeluxeKing, space, number), cancellationToken));

    private async Task SeedAmenitiesAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        await steps.EnsureAsync(
            "amenity Pool",
            AuroraGrand.Pool,
            () => amenities.GetAsync(AuroraGrand.Pool, cancellationToken),
            () => amenities.CreateAsync(new CreateAmenityRequest(AuroraGrand.PropertyId, "Pool", AmenityCategory.Pool, "Indoor heated pool, 18 metres, with loungers.", AuroraGrand.PoolHall), cancellationToken)).ConfigureAwait(false);
        await steps.EnsureAsync(
            "amenity Gym",
            AuroraGrand.Gym,
            () => amenities.GetAsync(AuroraGrand.Gym, cancellationToken),
            () => amenities.CreateAsync(new CreateAmenityRequest(AuroraGrand.PropertyId, "Gym", AmenityCategory.Fitness, "Cardio machines and free weights.", AuroraGrand.FitnessStudio), cancellationToken)).ConfigureAwait(false);

        DateOnly christmas = new(localToday.Year, 12, 25);
        await HoursAsync(
            "pool hours",
            AuroraGrand.PoolHours,
            new SubjectReference(SubjectKind.Amenity, AuroraGrand.Pool),
            Daily(new OpeningInterval(new TimeOnly(7, 0), new TimeOnly(22, 0))),
            [new HoursOverride(christmas, [new OpeningInterval(new TimeOnly(10, 0), new TimeOnly(14, 0))], "Christmas Day hours")],
            cancellationToken).ConfigureAwait(false);
        await HoursAsync(
            "gym hours",
            AuroraGrand.GymHours,
            new SubjectReference(SubjectKind.Amenity, AuroraGrand.Gym),
            Daily(new OpeningInterval(new TimeOnly(6, 0), new TimeOnly(23, 0))),
            [],
            cancellationToken).ConfigureAwait(false);
    }

    private Task HoursAsync(
        string record,
        Guid id,
        SubjectReference subject,
        ValueList<WeeklyHours> weekly,
        ValueList<HoursOverride> overrides,
        CancellationToken cancellationToken) =>
        SeedHours.EnsureAsync(steps, hours, record, id, subject, weekly, overrides, cancellationToken);

    private static ValueList<WeeklyHours> Daily(params OpeningInterval[] intervals) => SeedHours.Daily(intervals);

    private async Task SeedPoliciesAsync(CancellationToken cancellationToken)
    {
        await PolicyAsync(
            AuroraGrand.GymChildrenPolicy,
            PolicyType.Children,
            "Children in the gym",
            "Guests aged 12 to 15 may use the gym when accompanied by an adult. Children under 12 may not use the gym.",
            new SubjectReference(SubjectKind.Amenity, AuroraGrand.Gym),
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            AuroraGrand.PoolChildrenPolicy,
            PolicyType.Children,
            "Children in the pool",
            "Children under 16 must be supervised by an adult in the pool hall at all times. Children may swim from 07:00 to 19:00.",
            new SubjectReference(SubjectKind.Amenity, AuroraGrand.Pool),
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            AuroraGrand.PetsPolicy,
            PolicyType.Pets,
            "Pets",
            "Dogs up to 15 kg are welcome for GBP 30 per night. Please tell us before you arrive.",
            null,
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            AuroraGrand.CheckInCheckOutPolicy,
            PolicyType.CheckInCheckOut,
            "Check-in and check-out",
            "Check-in is from 15:00 and check-out is by 11:00. Late check-out until 13:00 can be requested at reception, subject to availability.",
            null,
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            AuroraGrand.SmokingPolicy,
            PolicyType.Smoking,
            "Smoking",
            "The Aurora Grand is non-smoking throughout, including balconies. A GBP 250 cleaning charge applies.",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PolicyAsync(Guid id, PolicyType type, string title, string text, SubjectReference? appliesTo, CancellationToken cancellationToken)
    {
        PolicyResponse policy = await steps.EnsureAsync(
            $"policy '{title}'",
            id,
            () => policies.GetAsync(id, cancellationToken),
            () => policies.CreateAsync(new CreatePolicyRequest(AuroraGrand.PropertyId, type, title, text, AppliesTo: appliesTo), cancellationToken)).ConfigureAwait(false);

        // Policies answer guests only once active; a draft left by an interrupted seed is activated now.
        if (policy.Status == PolicyStatus.Draft)
        {
            await SeedSteps.RequireAsync($"activating policy '{title}'", policies.ActivateAsync(id, policy.Version, cancellationToken)).ConfigureAwait(false);
        }
    }

    private async Task SeedContentAsync(CancellationToken cancellationToken)
    {
        await ContentAsync(
            AuroraGrand.OverviewContent,
            ContentType.Overview,
            "Welcome to the Aurora Grand",
            "A riverside hotel on the Strand with an indoor pool, a gym, Saffron for modern Indian dining, and Harbour Grill for seafood.",
            [],
            cancellationToken).ConfigureAwait(false);
        await ContentAsync(
            AuroraGrand.WiFiContent,
            ContentType.Faq,
            "How do I connect to the Wi-Fi?",
            "Join the Aurora-Guest network; the password is printed on your key card sleeve.",
            ["wifi", "internet"],
            cancellationToken).ConfigureAwait(false);
        await ContentAsync(
            AuroraGrand.ParkingContent,
            ContentType.Directions,
            "Parking",
            "Valet parking is at the Savoy Street entrance, GBP 45 per night. Leave your keys with the doorman.",
            ["parking", "car"],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ContentAsync(Guid id, ContentType type, string title, string body, ValueList<string> tags, CancellationToken cancellationToken)
    {
        ContentItemResponse item = await steps.EnsureAsync(
            $"content '{title}'",
            id,
            () => content.GetAsync(id, cancellationToken),
            () => content.CreateAsync(new CreateContentItemRequest(AuroraGrand.PropertyId, type, title, body, Tags: tags), cancellationToken)).ConfigureAwait(false);

        if (item.Status == ContentStatus.Draft)
        {
            await SeedSteps.RequireAsync($"publishing content '{title}'", content.PublishAsync(id, item.Version, cancellationToken)).ConfigureAwait(false);
        }
    }
}
