using HospitalityOS.Ai;
using HospitalityOS.Bindings;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Contracts.Events.Services;
using HospitalityOS.Eventing;
using HospitalityOS.Workflows.Concierge;
using HospitalityOS.Workflows.GuestService;
using HospitalityOS.Workflows.Routing;
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
    /// (<see cref="WorkRequester.Name"/>); and the HumanAction Router, which assigns new work, reroutes it when duty
    /// changes, and hands on work not accepted within <see cref="HumanActionRoutingOptions.AcceptanceTimeout"/>. Each that
    /// uses a model declares its task with its prompt. The host must also bind <c>IScheduler</c>.
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
            .AddEventHandler<HumanActionRequestedV1, NewWorkRouting>(NewWorkRouting.Name)
            .AddEventHandler<StaffMemberDutyChangedV1, DutyRerouting>(DutyRerouting.Name)
            .AddEventHandler<StaffMemberCreatedV1, DutyRerouting>(DutyRerouting.JoinedName)
            .AddEventHandler<HumanActionAssignedV1, AcceptanceWatch>(AcceptanceWatch.Name)
            .AddScheduledHandler<AcceptanceDue, UnacceptedRerouting>(UnacceptedRerouting.Name);
        services.AddOptions<HumanActionRoutingOptions>();
        services.TryAddScoped<HumanActionRouter>();

        return services;
    }
}
