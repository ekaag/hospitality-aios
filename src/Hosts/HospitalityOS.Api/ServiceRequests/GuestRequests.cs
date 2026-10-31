using System.Text.Json;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.ServiceRequests;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.ServiceRequests;

/// <summary>
/// A guest's unresolved requests (spec §33 service status), read through <c>GuestService.GetRequestStatus</c> (P7), the
/// same canonical status the concierge reports, so the status panel and the chat never disagree. A guest reads only their
/// own stay's requests, and only while the stay is active.
/// </summary>
internal sealed class GuestRequests(IStayContextRepository stays, ICapabilityExecutor executor)
{
    private const string Capability = "GuestService.GetRequestStatus";

    public async Task<Result<IReadOnlyList<GuestRequestResponse>>> ListAsync(GuestCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsActive(await stays.GetByIdAsync(caller.StayContextId, cancellationToken).ConfigureAwait(false)))
        {
            return ConversationErrors.StayNotActive;
        }

        Result<CapabilityResponse> read = await executor.ExecuteAsync(
            new CapabilityRequest(Capability, JsonSerializer.SerializeToElement(new GetRequestStatusInput(caller.StayContextId), ContractJson.Options), 1),
            cancellationToken).ConfigureAwait(false);

        if (read.IsFailure)
        {
            return read.Error;
        }

        GetRequestStatusOutput output = read.Value.Output.Deserialize<GetRequestStatusOutput>(ContractJson.Options)!;

        return Result.Success<IReadOnlyList<GuestRequestResponse>>([.. output.Requests.Select(Response).OfType<GuestRequestResponse>()]);
    }

    // The capability lists only unresolved requests; anything else is not the guest's to wait for.
    private static GuestRequestResponse? Response(ServiceRequestEntry entry) =>
        Status(entry.Status) is { } status
            ? new(new ServiceRequestId(entry.Source.Id), entry.ServiceName, entry.ItemName, entry.Quantity, entry.Description, entry.Location, status, entry.LocalRequestedAt)
            : null;

    private static GuestRequestStatus? Status(RequestStatus status) => status switch
    {
        RequestStatus.Open => GuestRequestStatus.Open,
        RequestStatus.InProgress => GuestRequestStatus.InProgress,
        RequestStatus.Exception => GuestRequestStatus.Exception,
        _ => null,
    };
}
