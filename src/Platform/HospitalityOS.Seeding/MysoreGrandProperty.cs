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
/// The Mysore Grand itself: organization, property, spaces, rooms, amenities and their hours, policies, and content.
/// </summary>
internal sealed class MysoreGrandProperty(
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
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await steps.EnsureAsync(
            "organization Mysore Heritage Hotels",
            MysoreGrand.OrganizationId.Value,
            () => organizations.GetAsync(MysoreGrand.OrganizationId, cancellationToken),
            () => organizations.CreateAsync(new CreateOrganizationRequest("Mysore Heritage Hotels"), cancellationToken)).ConfigureAwait(false);
        await steps.EnsureAsync(
            "property Mysore Grand",
            MysoreGrand.PropertyId.Value,
            () => properties.GetAsync(MysoreGrand.PropertyId, cancellationToken),
            () => properties.CreateAsync(
                new CreatePropertyRequest(
                    MysoreGrand.OrganizationId,
                    MysoreGrand.Name,
                    MysoreGrand.TimeZone,
                    new PropertyAddress("1 Palace Road", null, "Mysuru", "Karnataka", "570001", "IN"),
                    "+91 821 400 0000",
                    "stay@mysoregrand.example"),
                cancellationToken)).ConfigureAwait(false);

        await SeedSpacesAsync(cancellationToken).ConfigureAwait(false);
        await SeedRoomsAsync(cancellationToken).ConfigureAwait(false);
        await SeedAmenitiesAsync(cancellationToken).ConfigureAwait(false);
        await SeedPoliciesAsync(cancellationToken).ConfigureAwait(false);
        await SeedContentAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedSpacesAsync(CancellationToken cancellationToken)
    {
        await SpaceAsync(MysoreGrand.PalaceWing, null, "Palace Wing", SpaceKind.Building, null, cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.DurbarHall, MysoreGrand.PalaceWing, "Durbar Hall", SpaceKind.Area, "Through the Elephant Gate, under the stained-glass ceiling.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.AmbavilasLounge, MysoreGrand.PalaceWing, "Ambavilas Lounge", SpaceKind.Area, "On the right of Durbar Hall, past the brass doors.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.GardenLevel, MysoreGrand.PalaceWing, "Garden Level", SpaceKind.Floor, "Down the marble staircase from Durbar Hall.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.Courtyard, MysoreGrand.GardenLevel, "Courtyard", SpaceKind.Outdoor, "Through the arches at the foot of the staircase.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.SpaPavilion, MysoreGrand.GardenLevel, "Spa Pavilion", SpaceKind.Area, "Across the courtyard, past the pool.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.Floor2, MysoreGrand.PalaceWing, "Floor 2", SpaceKind.Floor, null, cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.AmbariDiningRoom, MysoreGrand.PalaceWing, "Ambari Dining Room", SpaceKind.Area, "Ground floor, at the end of Durbar Hall.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.RoofTerrace, MysoreGrand.PalaceWing, "Roof Terrace", SpaceKind.Outdoor, "Take the lift to R; the terrace faces Chamundi Hill.", cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.Room210Space, MysoreGrand.Floor2, "Room 210", SpaceKind.Room, null, cancellationToken).ConfigureAwait(false);
        await SpaceAsync(MysoreGrand.Room214Space, MysoreGrand.Floor2, "Room 214", SpaceKind.Room, null, cancellationToken).ConfigureAwait(false);
    }

    private Task<SpaceResponse> SpaceAsync(SpaceId id, SpaceId? parent, string name, SpaceKind kind, string? directions, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"Mysore Grand space '{name}'",
            id.Value,
            () => spaces.GetAsync(id, cancellationToken),
            () => spaces.CreateAsync(new CreateSpaceRequest(MysoreGrand.PropertyId, parent, name, kind, Directions: directions), cancellationToken));

    private async Task SeedRoomsAsync(CancellationToken cancellationToken)
    {
        await steps.EnsureAsync(
            "room type Maharaja Suite",
            MysoreGrand.MaharajaSuite,
            () => accommodations.GetTypeAsync(MysoreGrand.MaharajaSuite, cancellationToken),
            () => accommodations.CreateTypeAsync(new CreateAccommodationTypeRequest(MysoreGrand.PropertyId, "Maharaja Suite", 2, "King bed, carved rosewood, palace-garden view."), cancellationToken)).ConfigureAwait(false);
        await RoomAsync(MysoreGrand.Room210, MysoreGrand.Room210Space, "210", cancellationToken).ConfigureAwait(false);
        await RoomAsync(MysoreGrand.Room214, MysoreGrand.Room214Space, "214", cancellationToken).ConfigureAwait(false);
    }

    private Task<AccommodationUnitResponse> RoomAsync(AccommodationUnitId id, SpaceId space, string number, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"Mysore Grand room {number}",
            id.Value,
            () => accommodations.GetUnitAsync(id, cancellationToken),
            () => accommodations.CreateUnitAsync(new CreateAccommodationUnitRequest(MysoreGrand.MaharajaSuite, space, number), cancellationToken));

    private async Task SeedAmenitiesAsync(CancellationToken cancellationToken)
    {
        await steps.EnsureAsync(
            "Mysore Grand amenity Courtyard Pool",
            MysoreGrand.Pool,
            () => amenities.GetAsync(MysoreGrand.Pool, cancellationToken),
            () => amenities.CreateAsync(new CreateAmenityRequest(MysoreGrand.PropertyId, "Courtyard Pool", AmenityCategory.Pool, "Open-air pool in the palace courtyard, shaded by frangipani.", MysoreGrand.Courtyard), cancellationToken)).ConfigureAwait(false);
        await steps.EnsureAsync(
            "Mysore Grand amenity Ayurveda Spa",
            MysoreGrand.Spa,
            () => amenities.GetAsync(MysoreGrand.Spa, cancellationToken),
            () => amenities.CreateAsync(new CreateAmenityRequest(MysoreGrand.PropertyId, "Ayurveda Spa", AmenityCategory.Spa, "Abhyanga oil massage, shirodhara, and steam.", MysoreGrand.SpaPavilion), cancellationToken)).ConfigureAwait(false);

        await HoursAsync("Mysore Grand pool hours", MysoreGrand.PoolHours, new SubjectReference(SubjectKind.Amenity, MysoreGrand.Pool), SeedHours.Daily(new OpeningInterval(new TimeOnly(6, 30), new TimeOnly(20, 0))), cancellationToken).ConfigureAwait(false);
        await HoursAsync("Mysore Grand spa hours", MysoreGrand.SpaHours, new SubjectReference(SubjectKind.Amenity, MysoreGrand.Spa), SeedHours.Daily(new OpeningInterval(new TimeOnly(9, 0), new TimeOnly(21, 0))), cancellationToken).ConfigureAwait(false);
    }

    private Task HoursAsync(string record, Guid id, SubjectReference subject, ValueList<WeeklyHours> weekly, CancellationToken cancellationToken) =>
        SeedHours.EnsureAsync(steps, hours, MysoreGrand.PropertyId, record, id, subject, weekly, [], cancellationToken);

    private async Task SeedPoliciesAsync(CancellationToken cancellationToken)
    {
        await PolicyAsync(
            MysoreGrand.PoolChildrenPolicy,
            PolicyType.Children,
            "Children in the pool",
            "Children under 14 must be supervised by an adult in the courtyard pool at all times.",
            new SubjectReference(SubjectKind.Amenity, MysoreGrand.Pool),
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            MysoreGrand.PetsPolicy,
            PolicyType.Pets,
            "Pets",
            "Pets are not allowed, except assistance dogs.",
            null,
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            MysoreGrand.CheckInCheckOutPolicy,
            PolicyType.CheckInCheckOut,
            "Check-in and check-out",
            "Check-in is from 14:00 and check-out is by 12:00. Late check-out until 15:00 can be requested at reception, subject to availability.",
            null,
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            MysoreGrand.SmokingPolicy,
            PolicyType.Smoking,
            "Smoking",
            "The Mysore Grand is non-smoking throughout, including the terrace. An INR 10,000 cleaning charge applies.",
            null,
            cancellationToken).ConfigureAwait(false);
        await PolicyAsync(
            MysoreGrand.DressCodePolicy,
            PolicyType.DressCode,
            "Durbar Hall in the evening",
            "Smart casual in Durbar Hall after 19:00; shoulders covered for the Carnatic music evenings.",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PolicyAsync(Guid id, PolicyType type, string title, string text, SubjectReference? appliesTo, CancellationToken cancellationToken)
    {
        PolicyResponse policy = await steps.EnsureAsync(
            $"Mysore Grand policy '{title}'",
            id,
            () => policies.GetAsync(id, cancellationToken),
            () => policies.CreateAsync(new CreatePolicyRequest(MysoreGrand.PropertyId, type, title, text, AppliesTo: appliesTo), cancellationToken)).ConfigureAwait(false);

        // Policies answer guests only once active; a draft left by an interrupted seed is activated now.
        if (policy.Status == PolicyStatus.Draft)
        {
            await SeedSteps.RequireAsync($"activating Mysore Grand policy '{title}'", policies.ActivateAsync(id, policy.Version, cancellationToken)).ConfigureAwait(false);
        }
    }

    private async Task SeedContentAsync(CancellationToken cancellationToken)
    {
        await ContentAsync(
            MysoreGrand.OverviewContent,
            ContentType.Overview,
            "Welcome to the Mysore Grand",
            "A heritage palace hotel in the heart of Mysuru, a short walk from Mysore Palace: a courtyard pool, an Ayurveda spa, "
                + "Ambari for Mysore cuisine, and Chamundi Terrace for evening dining under the lit domes.",
            [],
            cancellationToken).ConfigureAwait(false);
        await ContentAsync(
            MysoreGrand.WiFiContent,
            ContentType.Faq,
            "How do I connect to the Wi-Fi?",
            "Join the MysoreGrand-Guest network; the password is printed on your key card sleeve.",
            ["wifi", "internet"],
            cancellationToken).ConfigureAwait(false);
        await ContentAsync(
            MysoreGrand.ArrivalContent,
            ContentType.Directions,
            "Arrival and parking",
            "Drive in through the Elephant Gate on Palace Road; valet parking is free for guests. Mysuru Junction station is 10 minutes away.",
            ["parking", "car", "arrival"],
            cancellationToken).ConfigureAwait(false);
        await ContentAsync(
            MysoreGrand.IlluminationContent,
            ContentType.Faq,
            "When is Mysore Palace lit up?",
            "Mysore Palace is illuminated by nearly 100,000 bulbs on Sundays and public holidays from 19:00 to 19:45, and every night during Dasara. "
                + "The roof terrace has the best view; the concierge runs a guided walk on illumination nights.",
            ["palace", "illumination", "dasara"],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ContentAsync(Guid id, ContentType type, string title, string body, ValueList<string> tags, CancellationToken cancellationToken)
    {
        ContentItemResponse item = await steps.EnsureAsync(
            $"Mysore Grand content '{title}'",
            id,
            () => content.GetAsync(id, cancellationToken),
            () => content.CreateAsync(new CreateContentItemRequest(MysoreGrand.PropertyId, type, title, body, Tags: tags), cancellationToken)).ConfigureAwait(false);

        if (item.Status == ContentStatus.Draft)
        {
            await SeedSteps.RequireAsync($"publishing Mysore Grand content '{title}'", content.PublishAsync(id, item.Version, cancellationToken)).ConfigureAwait(false);
        }
    }
}
