using HospitalityOS.Application.Events;
using HospitalityOS.Application.Hours;
using HospitalityOS.Application.Menus;
using HospitalityOS.Application.Restaurants;
using HospitalityOS.Application.Services;
using HospitalityOS.Application.Staff;
using HospitalityOS.Contracts.Api.Events;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.Contracts.Api.Menus;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Restaurants;
using HospitalityOS.Contracts.Api.Services;
using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.Seeding;

/// <summary>
/// What the Aurora Grand offers: restaurants with their hours and menus, tonight's and tomorrow's events, the services
/// guests can request, and the staff on duty to do them.
/// </summary>
internal sealed class AuroraGrandOfferings(
    SeedSteps steps,
    IRestaurantAdministration restaurants,
    IOperatingHoursAdministration hours,
    IMenuAdministration menus,
    IPropertyEventAdministration propertyEvents,
    IServiceAdministration services,
    IStaffAdministration staff)
{
    public async Task SeedAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        await SeedDiningAsync(cancellationToken).ConfigureAwait(false);
        await SeedEventsAsync(localToday, cancellationToken).ConfigureAwait(false);
        await SeedServicesAsync(cancellationToken).ConfigureAwait(false);
        await SeedStaffAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedDiningAsync(CancellationToken cancellationToken)
    {
        await RestaurantAsync(AuroraGrand.Saffron, "Saffron", "Indian", "Modern Indian dining with plenty of vegetarian and vegan dishes.", AuroraGrand.SaffronDiningRoom, cancellationToken).ConfigureAwait(false);
        await RestaurantAsync(AuroraGrand.HarbourGrill, "Harbour Grill", "Seafood", "Seafood and grill classics by the river.", AuroraGrand.HarbourGrillDiningRoom, cancellationToken).ConfigureAwait(false);

        await SeedHours.EnsureAsync(
            steps,
            hours,
            "Saffron hours",
            AuroraGrand.SaffronHours,
            new SubjectReference(SubjectKind.Restaurant, AuroraGrand.Saffron),
            SeedHours.Daily(new OpeningInterval(new TimeOnly(18, 0), new TimeOnly(23, 0))),
            [],
            cancellationToken).ConfigureAwait(false);
        await SeedHours.EnsureAsync(
            steps,
            hours,
            "Harbour Grill hours",
            AuroraGrand.HarbourGrillHours,
            new SubjectReference(SubjectKind.Restaurant, AuroraGrand.HarbourGrill),
            SeedHours.Daily(new OpeningInterval(new TimeOnly(12, 0), new TimeOnly(15, 0)), new OpeningInterval(new TimeOnly(18, 0), new TimeOnly(22, 0))),
            [],
            cancellationToken).ConfigureAwait(false);

        await MenuAsync(
            AuroraGrand.SaffronDinner,
            AuroraGrand.Saffron,
            "Dinner",
            "Served 18:00 to 23:00.",
            [
                Item(AuroraGrand.PaneerTikka, "Paneer tikka", "Chargrilled paneer, mint chutney.", 12.50m, "Starters", DietaryTag.Vegetarian, DietaryTag.GlutenFree),
                Item(AuroraGrand.VegetableBiryani, "Vegetable biryani", "Saffron rice, seasonal vegetables, cashews.", 18m, "Mains", DietaryTag.Vegan, DietaryTag.ContainsNuts),
                Item(AuroraGrand.LambRoganJosh, "Lamb rogan josh", "Slow-cooked Kashmiri lamb.", 24m, "Mains", DietaryTag.GlutenFree, DietaryTag.Spicy),
                Item(AuroraGrand.TarkaDal, "Tarka dal", "Yellow lentils, garlic, cumin.", 14m, "Mains", DietaryTag.Vegan, DietaryTag.GlutenFree),
                Item(AuroraGrand.MangoKulfi, "Mango kulfi", "Alphonso mango ice cream.", 8m, "Desserts", DietaryTag.Vegetarian, DietaryTag.GlutenFree),
            ],
            cancellationToken).ConfigureAwait(false);
        await MenuAsync(
            AuroraGrand.HarbourGrillLunch,
            AuroraGrand.HarbourGrill,
            "Lunch",
            "Served 12:00 to 15:00.",
            [
                Item(AuroraGrand.FishAndChips, "Fish and chips", "Beer-battered haddock, mushy peas.", 21m, "Mains"),
                Item(AuroraGrand.HalloumiBurger, "Halloumi burger", "Grilled halloumi, roasted pepper, fries.", 17m, "Mains", DietaryTag.Vegetarian),
                Item(AuroraGrand.SeafoodLinguine, "Seafood linguine", "Prawns, mussels, chilli, garlic.", 26m, "Mains", DietaryTag.ContainsShellfish),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private Task<RestaurantResponse> RestaurantAsync(Guid id, string name, string cuisine, string description, SpaceId location, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"restaurant {name}",
            id,
            () => restaurants.GetAsync(id, cancellationToken),
            () => restaurants.CreateAsync(new CreateRestaurantRequest(AuroraGrand.PropertyId, name, cuisine, description, location), cancellationToken));

    private Task<MenuResponse> MenuAsync(Guid id, Guid restaurant, string name, string description, (Guid Id, MenuItemRequest Request)[] items, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"menu {name}",
            [id, .. items.Select(item => item.Id)],
            () => menus.GetAsync(id, cancellationToken),
            () => menus.CreateAsync(new CreateMenuRequest(restaurant, name, description, ValueList.From(items.Select(item => item.Request))), cancellationToken));

    // New items carry no ID in the request; the service gives each one the next reserved ID, in order.
    private static (Guid Id, MenuItemRequest Request) Item(Guid id, string name, string description, decimal price, string category, params DietaryTag[] dietary) =>
        (id, new MenuItemRequest(Name: name, Description: description, Price: new Money(price, "GBP"), Category: category, Dietary: ValueList.From(dietary)));

    private async Task SeedEventsAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        DateOnly tomorrow = localToday.AddDays(1);

        await EventAsync(
            AuroraGrand.JazzNight,
            new CreatePropertyEventRequest(
                AuroraGrand.PropertyId,
                "Jazz night",
                PropertyEventCategory.Entertainment,
                EventAudience.Everyone,
                At(localToday, 20, 0),
                At(localToday, 23, 0),
                "Live trio in the lobby bar. Free entry.",
                AuroraGrand.LobbyBar),
            cancellationToken).ConfigureAwait(false);
        await EventAsync(
            AuroraGrand.WineTasting,
            new CreatePropertyEventRequest(
                AuroraGrand.PropertyId,
                "Wine tasting",
                PropertyEventCategory.FoodAndDrink,
                EventAudience.AdultsOnly,
                At(localToday, 18, 30),
                At(localToday, 19, 30),
                "Six English sparkling wines with our sommelier. Book at reception.",
                AuroraGrand.LobbyBar,
                new Money(25m, "GBP"),
                BookingRequired: true),
            cancellationToken).ConfigureAwait(false);
        await EventAsync(
            AuroraGrand.MorningYoga,
            new CreatePropertyEventRequest(
                AuroraGrand.PropertyId,
                "Morning yoga",
                PropertyEventCategory.Wellness,
                EventAudience.Everyone,
                At(tomorrow, 7, 30),
                At(tomorrow, 8, 30),
                "Gentle flow for all levels; mats provided.",
                AuroraGrand.FitnessStudio),
            cancellationToken).ConfigureAwait(false);
        await EventAsync(
            AuroraGrand.KidsClub,
            new CreatePropertyEventRequest(
                AuroraGrand.PropertyId,
                "Kids club",
                PropertyEventCategory.Family,
                EventAudience.Children,
                At(tomorrow, 15, 0),
                At(tomorrow, 17, 0),
                "Crafts and games for ages 5 to 11."),
            cancellationToken).ConfigureAwait(false);
    }

    private Task<PropertyEventResponse> EventAsync(Guid id, CreatePropertyEventRequest request, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"event '{request.Title}'",
            id,
            () => propertyEvents.GetAsync(id, cancellationToken),
            () => propertyEvents.CreateAsync(request, cancellationToken));

    // Property-local wall-clock time (no offset), as event times are.
    private static DateTime At(DateOnly date, int hour, int minute) => date.ToDateTime(new TimeOnly(hour, minute));

    private async Task SeedServicesAsync(CancellationToken cancellationToken)
    {
        await ServiceAsync(
            AuroraGrand.HousekeepingAmenities,
            new CreateServiceRequest(
                AuroraGrand.PropertyId,
                "housekeeping.amenities",
                "Housekeeping amenities",
                ServiceCategory.Housekeeping,
                "Towels, pillows, and toiletries delivered to your room.",
                IsGuestRequestable: true,
                Items:
                [
                    new ServiceItemRequest("bath_towel", "Bath towel", 4),
                    new ServiceItemRequest("hand_towel", "Hand towel", 4),
                    new ServiceItemRequest("pillow", "Pillow", 2),
                    new ServiceItemRequest("toiletries_kit", "Toiletries kit", 2),
                ],
                TargetCompletion: TimeSpan.FromMinutes(30)),
            cancellationToken).ConfigureAwait(false);
        await ServiceAsync(
            AuroraGrand.MaintenanceRepair,
            new CreateServiceRequest(
                AuroraGrand.PropertyId,
                "maintenance.repair",
                "Repairs",
                ServiceCategory.Maintenance,
                "Something in your room not working, such as the air conditioning, lights, or TV.",
                IsGuestRequestable: true,
                TargetCompletion: TimeSpan.FromHours(2)),
            cancellationToken).ConfigureAwait(false);
    }

    private Task<ServiceResponse> ServiceAsync(Guid id, CreateServiceRequest request, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"service {request.Code}",
            id,
            () => services.GetAsync(id, cancellationToken),
            () => services.CreateAsync(request, cancellationToken));

    // Two housekeepers, so the router has a choice to balance, and one engineer.
    private async Task SeedStaffAsync(CancellationToken cancellationToken)
    {
        await StaffMemberAsync(AuroraGrand.SamPatel, "Sam Patel", "housekeeping", cancellationToken).ConfigureAwait(false);
        await StaffMemberAsync(AuroraGrand.JoOkafor, "Jo Okafor", "housekeeping", cancellationToken).ConfigureAwait(false);
        await StaffMemberAsync(AuroraGrand.RaviMehta, "Ravi Mehta", "maintenance", cancellationToken).ConfigureAwait(false);
    }

    private Task<StaffMemberResponse> StaffMemberAsync(StaffMemberId id, string name, string skill, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"staff member {name}",
            id.Value,
            () => staff.GetAsync(id.Value, cancellationToken),
            () => staff.CreateAsync(new CreateStaffMemberRequest(PropertyId: AuroraGrand.PropertyId, DisplayName: name, Skills: [skill], IsOnDuty: true), cancellationToken));
}
