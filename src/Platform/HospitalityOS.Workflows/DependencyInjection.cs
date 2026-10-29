using HospitalityOS.Ai;
using HospitalityOS.Bindings;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Eventing;
using HospitalityOS.Workflows.Concierge;
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
    /// Registers the Concierge Controller, which answers <c>GuestMessageReceived</c> with grounded answers, and declares
    /// its task (<see cref="Concierge.Concierge.AnswerTask"/>) with its prompt and tool allow-list.
    /// </summary>
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services
            .AddPrompts(typeof(DependencyInjection).Assembly)
            .AddGroundedAnswering(Concierge.Concierge.AnswerTask, Concierge.Concierge.AnswerTask, Concierge.Concierge.Tools)
            .AddEventHandler<GuestMessageReceivedV1, ConciergeController>(ConciergeController.OnGuestMessageReceived);

        return services;
    }
}
