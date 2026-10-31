using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.GuestService;

/// <summary>
/// Input of <c>GuestService.GetRequestStatus</c>.
/// </summary>
/// <param name="StayContextId">The guest's stay.</param>
public sealed record GetRequestStatusInput(
    [property: Description("The guest's stay.")] StayContextId StayContextId);
