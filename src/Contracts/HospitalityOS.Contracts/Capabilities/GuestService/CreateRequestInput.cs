using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.GuestService;

/// <summary>
/// Input of <c>GuestService.CreateRequest</c>.
/// </summary>
/// <param name="StayContextId">The in-house stay the request is for; its room is where the work is done.</param>
/// <param name="ServiceCode">The service, by code.</param>
/// <param name="ItemCode">The item, for services with items.</param>
/// <param name="Quantity">How many; 1 unless the guest said otherwise.</param>
/// <param name="Description">What is needed, in the guest's words; required for services without items.</param>
/// <param name="SourceMessageId">The guest message asking for it; at most one request is created per message.</param>
public sealed record CreateRequestInput(
    [property: Description("The in-house stay the request is for; its room is where the work is done.")] StayContextId StayContextId,
    [property: Description("The service, by code, e.g. housekeeping.amenities.")] string ServiceCode,
    [property: Description("The item, for services with items, e.g. bath_towel.")] string? ItemCode = null,
    [property: Description("How many; 1 unless the guest said otherwise.")] int Quantity = 1,
    [property: Description("What is needed, in the guest's words; required for services without items.")] string? Description = null,
    [property: Description("The guest message asking for it; at most one request is created per message.")] GuestMessageId? SourceMessageId = null);
