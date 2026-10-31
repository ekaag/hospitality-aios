using HospitalityOS.Ai;
using HospitalityOS.Bindings;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Contracts.Events.Services;
using HospitalityOS.Eventing;
using HospitalityOS.Workflows.Concierge;
using HospitalityOS.Workflows.GuestService;
using HospitalityOS.Workflows.GuestSurface;
using HospitalityOS.Workflows.Routing;
using HospitalityOS.Workflows.StaffSurface;
using HospitalityOS.Workflows.Triage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Workflows;

/// <summary>
/// Registers the autonomous controllers (spec §9), each subscribed to the events that start its work (P1). The host must
/// also call <c>AddInference</c>, <c>AddEventing</c>, <c>AddCapabilities</c> with <c>AddBuiltInCapabilities</c>, and
/// register persistence and <c>TenantContext</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the controllers for guest messages: triage, which classifies each message once by intent
    /// (<see cref="GuestTriage.Task"/>); the Concierge Controller, which answers questions and status checks with grounded
    /// answers (<see cref="Concierge.Concierge.AnswerTask"/>); and the Guest Service Controller, which turns service requests
    /// into canonical requests (<see cref="GuestServiceController.Task"/>) and asks staff to do the work each request needs
    /// (<see cref="WorkRequester.Name"/>) and moves each request on as that work starts, completes, or fails
    /// (<see cref="RequestContinuation"/>), telling the guest once it is done (<see cref="CompletionNotifier.Name"/>); and the
    /// HumanAction Router, which assigns new work, reroutes it when duty
    /// changes, and hands on work not accepted within <see cref="HumanActionRoutingOptions.AcceptanceTimeout"/>. Each that
    /// uses a model declares its task with its prompt; and the staff surface's notifier, which tells connected staff when work
    /// changes (<see cref="WorkChangeNotifier"/>), and the guest surface's, which tells a connected guest when their stay's
    /// messages or requests change (<see cref="StayChangeNotifier"/>). The host must also bind <c>IScheduler</c> and <c>IRealtimeNotifier</c>.
    /// </summary>
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services
            .AddPrompts(typeof(DependencyInjection).Assembly)
            .AddClassification(GuestTriage.Task, GuestTriage.Task)
            .AddGroundedAnswering(Concierge.Concierge.AnswerTask, Concierge.Concierge.AnswerTask, Concierge.Concierge.Tools)
            .AddExtraction(GuestServiceController.Task, GuestServiceController.Task)
            .AddEventHandler<GuestMessageReceivedV1, GuestTriage>(GuestTriage.Task)
            .AddEventHandler<GuestMessageTriagedV1, ConciergeController>(ConciergeController.OnGuestMessageTriaged)
            .AddEventHandler<GuestMessageTriagedV1, GuestServiceController>(GuestServiceController.Task)
            .AddEventHandler<ServiceRequestCreatedV1, WorkRequester>(WorkRequester.Name)
            .AddEventHandler<HumanActionStartedV1, RequestContinuation>(RequestContinuation.OnStarted)
            .AddEventHandler<HumanActionCompletedV1, RequestContinuation>(RequestContinuation.OnCompleted)
            .AddEventHandler<HumanActionFailedV1, RequestContinuation>(RequestContinuation.OnFailed)
            .AddEventHandler<HumanActionCancelledV1, RequestContinuation>(RequestContinuation.OnCancelled)
            .AddEventHandler<HumanActionExpiredV1, RequestContinuation>(RequestContinuation.OnExpired)
            .AddEventHandler<ServiceRequestCompletedV1, CompletionNotifier>(CompletionNotifier.Name)
            .AddEventHandler<HumanActionRequestedV1, NewWorkRouting>(NewWorkRouting.Name)
            .AddEventHandler<StaffMemberDutyChangedV1, DutyRerouting>(DutyRerouting.Name)
            .AddEventHandler<StaffMemberCreatedV1, DutyRerouting>(DutyRerouting.JoinedName)
            .AddEventHandler<HumanActionAssignedV1, AcceptanceWatch>(AcceptanceWatch.Name)
            .AddScheduledHandler<AcceptanceDue, UnacceptedRerouting>(UnacceptedRerouting.Name)
            .AddEventHandler<HumanActionRequestedV1, WorkChangeNotifier>(WorkChangeNotifier.OnRequested)
            .AddEventHandler<HumanActionAssignedV1, WorkChangeNotifier>(WorkChangeNotifier.OnAssigned)
            .AddEventHandler<HumanActionAcceptedV1, WorkChangeNotifier>(WorkChangeNotifier.OnAccepted)
            .AddEventHandler<HumanActionStartedV1, WorkChangeNotifier>(WorkChangeNotifier.OnStarted)
            .AddEventHandler<HumanActionCompletedV1, WorkChangeNotifier>(WorkChangeNotifier.OnCompleted)
            .AddEventHandler<HumanActionFailedV1, WorkChangeNotifier>(WorkChangeNotifier.OnFailed)
            .AddEventHandler<HumanActionCancelledV1, WorkChangeNotifier>(WorkChangeNotifier.OnCancelled)
            .AddEventHandler<HumanActionExpiredV1, WorkChangeNotifier>(WorkChangeNotifier.OnExpired)
            .AddEventHandler<GuestMessageReceivedV1, StayChangeNotifier>(StayChangeNotifier.OnMessageReceived)
            .AddEventHandler<GuestMessageSentV1, StayChangeNotifier>(StayChangeNotifier.OnMessageSent)
            .AddEventHandler<ServiceRequestCreatedV1, StayChangeNotifier>(StayChangeNotifier.OnRequestCreated)
            .AddEventHandler<ServiceRequestUpdatedV1, StayChangeNotifier>(StayChangeNotifier.OnRequestUpdated)
            .AddEventHandler<ServiceRequestCompletedV1, StayChangeNotifier>(StayChangeNotifier.OnRequestCompleted);
        services.AddOptions<HumanActionRoutingOptions>();
        services.TryAddScoped<HumanActionRouter>();

        return services;
    }
}
