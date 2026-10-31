using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.HumanActions;

/// <summary>
/// <c>HumanAction.Create</c>: asks staff to do physical work the AI cannot (spec §7, P2), raising
/// <c>HumanActionRequested</c> for the router. The caller is recorded as who requested it; work for a service request must
/// be at that request's property, and the caller's flow is the work's, which acting on it continues (P11). The idempotency
/// key makes a repeated request create the work once (P8).
/// </summary>
internal sealed class CreateActionCapability(
    IPropertyRepository properties,
    IServiceRequestRepository requests,
    IHumanActionRepository actions,
    OperationContext operation,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : ICapability<CreateActionInput, HumanActionOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "HumanAction.Create",
        Version = 1,
        Description = "Asks staff to do physical work, such as delivering items to a room or checking a reported problem, with what to do, "
            + "where, why, how urgently, and by when. The work is then routed to someone who can do it; no approval is needed.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Manager, CapabilityCaller.Admin),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.KeyRequired,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<HumanActionOutput>> ExecuteAsync(CreateActionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!Enum.IsDefined(input.ActionType))
        {
            return HumanActionErrors.Invalid("actionType", input.ActionType.ToString());
        }

        if (!Enum.IsDefined(input.Priority))
        {
            return HumanActionErrors.Invalid("priority", input.Priority.ToString());
        }

        Property? property = await properties.GetByIdAsync(input.PropertyId, cancellationToken).ConfigureAwait(false);

        if (property is null)
        {
            return ReadErrors.PropertyNotFound;
        }

        if (input.ServiceRequestId is { } requestId
            && (await requests.GetByIdAsync(requestId, cancellationToken).ConfigureAwait(false))?.PropertyId != property.Id)
        {
            return HumanActionErrors.ServiceRequestNotFound;
        }

        HumanActionRequest work = new(
            EnumMirror.To<HumanActionType>(input.ActionType),
            input.Instructions,
            input.Location,
            input.Reason,
            HumanActionEntries.Name(operation.Actor),
            EnumMirror.To<HumanActionPriority>(input.Priority),
            input.SlaMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            input.Deadline,
            input.Items is null ? null : ValueList.From(input.Items.Select(item => item is null ? null! : new ActionItem(item.Name, item.Quantity, item.Code))),
            input.SkillsRequired,
            input.ServiceRequestId,
            input.SourceEventId,
            operation.CorrelationId);
        Result<HumanAction> requested = HumanAction.Request(
            new HumanActionId(idGenerators.Resolve().NewId()), property.TenantId, property.Id, work, timeProvider.GetUtcNow());

        if (requested.IsFailure)
        {
            return requested.Error;
        }

        await actions.AddAsync(requested.Value, cancellationToken).ConfigureAwait(false);

        return HumanActionEntries.Output(requested.Value);
    }
}
