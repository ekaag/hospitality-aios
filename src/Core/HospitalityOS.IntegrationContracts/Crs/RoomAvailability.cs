using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.IntegrationContracts.Crs;

/// <summary>
/// One accommodation type's availability for a stay, as the CRS reports it.
/// </summary>
/// <param name="AccommodationTypeId">The provider's ID for the accommodation type.</param>
/// <param name="Name">The accommodation type's name, e.g. <c>Deluxe King</c>.</param>
/// <param name="AvailableUnits">How many units are free for every night.</param>
/// <param name="NightlyRate">The best available nightly rate; <c>null</c> when the provider does not quote one.</param>
public sealed record RoomAvailability(ExternalId AccommodationTypeId, string Name, int AvailableUnits, Money? NightlyRate);
