using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.GuestService;

/// <summary>
/// Expected failures of the guest service capabilities.
/// </summary>
internal static class GuestServiceErrors
{
    public static Error StayNotFound { get; } = Error.NotFound("guestservice.stay_not_found", "There is no such stay.");

    public static Error StayNotInHouse { get; } =
        Error.InvalidState("guestservice.stay_not_in_house", "The guest is not in house, so there is no room to deliver to or work in.");

    public static Error ServiceNotFound(string code) =>
        Error.Validation("serviceCode", "guestservice.service_not_found", $"The property has no service '{code}'.");

    public static Error MessageNotFound { get; } =
        Error.Validation("sourceMessageId", "guestservice.message_not_found", "There is no such guest message.");
}
