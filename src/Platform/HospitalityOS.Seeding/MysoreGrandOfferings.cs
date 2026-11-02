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
/// What the Mysore Grand offers: restaurants with their hours and menus (priced in rupees), tonight's and tomorrow's
/// events, the services guests can request, and the staff on duty to do them.
/// </summary>
internal sealed class MysoreGrandOfferings(
    SeedSteps steps,
    IRestaurantAdministration restaurants,
    IOperatingHoursAdministration hours,
    IMenuAdministration menus,
    IPropertyEventAdministration propertyEvents,
    IServiceAdministration services,
    IStaffAdministration staff)
{
    private const string Rupees = "INR";

    public async Task SeedAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        await SeedDiningAsync(cancellationToken).ConfigureAwait(false);
        await SeedEventsAsync(localToday, cancellationToken).ConfigureAwait(false);
        await SeedServicesAsync(cancellationToken).ConfigureAwait(false);
        await SeedStaffAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedDiningAsync(CancellationToken cancellationToken)
    {
        await RestaurantAsync(MysoreGrand.Ambari, "Ambari", "South Indian", "Mysore and Karnataka classics; lunch is served on banana leaf.", MysoreGrand.AmbariDiningRoom, cancellationToken).ConfigureAwait(false);
        await RestaurantAsync(MysoreGrand.ChamundiTerrace, "Chamundi Terrace", "Tandoor and grill", "Rooftop tandoor and grill facing Chamundi Hill.", MysoreGrand.RoofTerrace, cancellationToken).ConfigureAwait(false);

        await SeedHours.EnsureAsync(
            steps,
            hours,
            MysoreGrand.PropertyId,
            "Ambari hours",
            MysoreGrand.AmbariHours,
            new SubjectReference(SubjectKind.Restaurant, MysoreGrand.Ambari),
            SeedHours.Daily(
                new OpeningInterval(new TimeOnly(7, 0), new TimeOnly(10, 30)),
                new OpeningInterval(new TimeOnly(12, 30), new TimeOnly(15, 30)),
                new OpeningInterval(new TimeOnly(19, 0), new TimeOnly(23, 0))),
            [],
            cancellationToken).ConfigureAwait(false);
        await SeedHours.EnsureAsync(
            steps,
            hours,
            MysoreGrand.PropertyId,
            "Chamundi Terrace hours",
            MysoreGrand.ChamundiTerraceHours,
            new SubjectReference(SubjectKind.Restaurant, MysoreGrand.ChamundiTerrace),
            SeedHours.Daily(new OpeningInterval(new TimeOnly(18, 30), new TimeOnly(23, 30))),
            [],
            cancellationToken).ConfigureAwait(false);

        await MenuAsync(
            MysoreGrand.AmbariDinner,
            MysoreGrand.Ambari,
            "Dinner",
            "Served 19:00 to 23:00.",
            [
                Item(MysoreGrand.MysoreMasalaDosa, "Mysore masala dosa", "Crisp dosa, red chutney, potato palya, coconut chutney.", 280m, "Mains", DietaryTag.Vegetarian),
                Item(MysoreGrand.BisiBeleBath, "Bisi bele bath", "Rice, lentils, and vegetables with spiced tamarind, ghee, and boondi.", 320m, "Mains", DietaryTag.Vegetarian, DietaryTag.Spicy),
                Item(MysoreGrand.ChickenGheeRoast, "Chicken ghee roast", "Mangalorean roast in ghee and Byadgi chilli.", 520m, "Mains", DietaryTag.GlutenFree, DietaryTag.Spicy),
                Item(MysoreGrand.MysorePak, "Mysore pak", "The palace kitchens' gram-flour and ghee sweet.", 180m, "Desserts", DietaryTag.Vegetarian, DietaryTag.GlutenFree),
                Item(MysoreGrand.FilterCoffee, "Filter coffee", "Chikmagalur beans, frothed with hot milk.", 120m, "Drinks", DietaryTag.Vegetarian, DietaryTag.GlutenFree),
            ],
            cancellationToken).ConfigureAwait(false);
        await MenuAsync(
            MysoreGrand.ChamundiTerraceDinner,
            MysoreGrand.ChamundiTerrace,
            "Dinner",
            "Served 18:30 to 23:30.",
            [
                Item(MysoreGrand.TandooriPrawns, "Tandoori prawns", "Tiger prawns, ajwain, lime.", 890m, "Mains", DietaryTag.ContainsShellfish, DietaryTag.GlutenFree),
                Item(MysoreGrand.PaneerTikka, "Paneer tikka", "Chargrilled paneer, mint chutney.", 450m, "Starters", DietaryTag.Vegetarian, DietaryTag.GlutenFree),
                Item(MysoreGrand.MuttonPepperFry, "Mutton pepper fry", "Coorg-style, black pepper and curry leaves.", 690m, "Mains", DietaryTag.Spicy, DietaryTag.GlutenFree),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private Task<RestaurantResponse> RestaurantAsync(Guid id, string name, string cuisine, string description, SpaceId location, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"Mysore Grand restaurant {name}",
            id,
            () => restaurants.GetAsync(id, cancellationToken),
            () => restaurants.CreateAsync(new CreateRestaurantRequest(MysoreGrand.PropertyId, name, cuisine, description, location), cancellationToken));

    private Task<MenuResponse> MenuAsync(Guid id, Guid restaurant, string name, string description, (Guid Id, MenuItemRequest Request)[] items, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"Mysore Grand menu {name}",
            [id, .. items.Select(item => item.Id)],
            () => menus.GetAsync(id, cancellationToken),
            () => menus.CreateAsync(new CreateMenuRequest(restaurant, name, description, ValueList.From(items.Select(item => item.Request))), cancellationToken));

    // New items carry no ID in the request; the service gives each one the next reserved ID, in order.
    private static (Guid Id, MenuItemRequest Request) Item(Guid id, string name, string description, decimal price, string category, params DietaryTag[] dietary) =>
        (id, new MenuItemRequest(Name: name, Description: description, Price: new Money(price, Rupees), Category: category, Dietary: ValueList.From(dietary)));

    private async Task SeedEventsAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        DateOnly tomorrow = localToday.AddDays(1);

        await EventAsync(
            MysoreGrand.IlluminationWalk,
            new CreatePropertyEventRequest(
                MysoreGrand.PropertyId,
                "Palace illumination walk",
                PropertyEventCategory.Tour,
                EventAudience.Everyone,
                At(localToday, 18, 45),
                At(localToday, 20, 0),
                "A guided walk to Mysore Palace to see it lit by nearly 100,000 bulbs. Meet in Durbar Hall.",
                MysoreGrand.DurbarHall),
            cancellationToken).ConfigureAwait(false);
        await EventAsync(
            MysoreGrand.CarnaticEvening,
            new CreatePropertyEventRequest(
                MysoreGrand.PropertyId,
                "Carnatic music evening",
                PropertyEventCategory.Entertainment,
                EventAudience.Everyone,
                At(localToday, 20, 30),
                At(localToday, 22, 0),
                "Veena and mridangam in Durbar Hall. Free for guests.",
                MysoreGrand.DurbarHall),
            cancellationToken).ConfigureAwait(false);
        await EventAsync(
            MysoreGrand.SunriseYoga,
            new CreatePropertyEventRequest(
                MysoreGrand.PropertyId,
                "Sunrise yoga",
                PropertyEventCategory.Wellness,
                EventAudience.Everyone,
                At(tomorrow, 6, 0),
                At(tomorrow, 7, 0),
                "Ashtanga in the Mysore tradition, on the roof terrace; mats provided.",
                MysoreGrand.RoofTerrace),
            cancellationToken).ConfigureAwait(false);
        await EventAsync(
            MysoreGrand.ChamundiHillExcursion,
            new CreatePropertyEventRequest(
                MysoreGrand.PropertyId,
                "Chamundi Hill excursion",
                PropertyEventCategory.Tour,
                EventAudience.Everyone,
                At(tomorrow, 8, 0),
                At(tomorrow, 12, 0),
                "By car to the Chamundeshwari Temple and the Nandi statue, with a guide. Book at reception.",
                null,
                new Money(1500m, Rupees),
                BookingRequired: true),
            cancellationToken).ConfigureAwait(false);
    }

    private Task<PropertyEventResponse> EventAsync(Guid id, CreatePropertyEventRequest request, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"Mysore Grand event '{request.Title}'",
            id,
            () => propertyEvents.GetAsync(id, cancellationToken),
            () => propertyEvents.CreateAsync(request, cancellationToken));

    // Property-local wall-clock time (no offset), as event times are.
    private static DateTime At(DateOnly date, int hour, int minute) => date.ToDateTime(new TimeOnly(hour, minute));

    private async Task SeedServicesAsync(CancellationToken cancellationToken)
    {
        await ServiceAsync(
            MysoreGrand.HousekeepingAmenities,
            new CreateServiceRequest(
                MysoreGrand.PropertyId,
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
            MysoreGrand.MaintenanceRepair,
            new CreateServiceRequest(
                MysoreGrand.PropertyId,
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
            $"Mysore Grand service {request.Code}",
            id,
            () => services.GetAsync(id, cancellationToken),
            () => services.CreateAsync(request, cancellationToken));

    private async Task SeedStaffAsync(CancellationToken cancellationToken)
    {
        await StaffMemberAsync(MysoreGrand.LakshmiIyer, "Lakshmi Iyer", "housekeeping", cancellationToken).ConfigureAwait(false);
        await StaffMemberAsync(MysoreGrand.KiranGowda, "Kiran Gowda", "housekeeping", cancellationToken).ConfigureAwait(false);
        await StaffMemberAsync(MysoreGrand.ArjunNair, "Arjun Nair", "maintenance", cancellationToken).ConfigureAwait(false);
    }

    private Task<StaffMemberResponse> StaffMemberAsync(StaffMemberId id, string name, string skill, CancellationToken cancellationToken) =>
        steps.EnsureAsync(
            $"Mysore Grand staff member {name}",
            id.Value,
            () => staff.GetAsync(id.Value, cancellationToken),
            () => staff.CreateAsync(new CreateStaffMemberRequest(PropertyId: MysoreGrand.PropertyId, DisplayName: name, Skills: [skill], IsOnDuty: true), cancellationToken));
}
