using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Work;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Work;

/// <summary>
/// The staff surface's work (spec §32, Journeys 13, 14, 20): the four views of a property's human actions, each piece of
/// work with what the caller may do to it now, and those steps. A worker is the staff member their token names and sees
/// their own work and the unassigned work they may claim; managers and administrators see everyone's. Every step runs a
/// capability through the executor (P7), which authorizes, audits, and commits it, continuing the work's own flow so the
/// journey keeps one correlation ID (P11). Each capability runs in a scope of its own, so the work is read back in a fresh
/// scope after a step.
/// </summary>
internal sealed class StaffWork(
    IHumanActionRepository actions,
    IStaffMemberRepository staff,
    IServiceRequestRepository requests,
    ICapabilityExecutor executor,
    OperationContext operation,
    ICurrentTenant currentTenant,
    IServiceScopeFactory scopes,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How far back the completed view reaches.
    /// </summary>
    public static TimeSpan CompletedWindow { get; } = TimeSpan.FromDays(1);

    public static Error NotOnRoster { get; } =
        Error.Forbidden("work.not_on_roster", "Only a staff member on this property's roster, or a manager, can see its work.");

    public static Error NotFound { get; } = Error.NotFound("work.not_found", "There is no such work.");

    public static Error NotAnException { get; } = Error.InvalidState(
        "work.not_an_exception", "Only the latest failed, expired, or cancelled work for a request still waiting for new work can be requested again.");

    public async Task<Result<IReadOnlyList<WorkItemResponse>>> ListAsync(PropertyId propertyId, WorkView view, CancellationToken cancellationToken)
    {
        Result<Caller> found = await CallerAsync(propertyId, cancellationToken).ConfigureAwait(false);

        if (found.IsFailure)
        {
            return found.Error;
        }

        Caller caller = found.Value;
        IReadOnlyList<HumanAction> listed = view switch
        {
            WorkView.Assigned => [.. (await actions.ListOpenForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false))
                .Where(action => action.Status != HumanActionStatus.Requested && caller.Sees(action))],
            WorkView.Unassigned => [.. (await actions.ListOpenForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false))
                .Where(action => action.Status == HumanActionStatus.Requested && (caller.Oversees || caller.MayClaim(action)))],
            WorkView.Completed => [.. (await actions.ListEndedForPropertyAsync(propertyId, timeProvider.GetUtcNow() - CompletedWindow, cancellationToken).ConfigureAwait(false))
                .Where(action => action.Status == HumanActionStatus.Completed && caller.Sees(action))],
            WorkView.Exceptions => [.. (await ExceptionsAsync(propertyId, cancellationToken).ConfigureAwait(false)).Where(caller.Sees)],
            _ => [],
        };

        HashSet<HumanActionId> exceptions = view == WorkView.Exceptions ? [.. listed.Select(action => action.Id)] : [];

        return Result.Success<IReadOnlyList<WorkItemResponse>>([.. listed.Select(action => Item(action, caller, exceptions.Contains(action.Id)))]);
    }

    public Task<Result<WorkItemResponse>> AcceptAsync(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, "HumanAction.UpdateStatus", new UpdateActionStatusInput(id, ActionStatus.Accepted), cancellationToken);

    public Task<Result<WorkItemResponse>> StartAsync(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, "HumanAction.UpdateStatus", new UpdateActionStatusInput(id, ActionStatus.InProgress), cancellationToken);

    public Task<Result<WorkItemResponse>> CompleteAsync(Guid id, CompleteWorkRequest request, CancellationToken cancellationToken) =>
        ActAsync(id, "HumanAction.Complete", new CompleteActionInput(id, request?.Note), cancellationToken);

    public Task<Result<WorkItemResponse>> FailAsync(Guid id, FailWorkRequest request, CancellationToken cancellationToken) =>
        ActAsync(id, "HumanAction.UpdateStatus", new UpdateActionStatusInput(id, ActionStatus.Failed, request?.Reason), cancellationToken);

    public Task<Result<WorkItemResponse>> ClaimAsync(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, "HumanAction.Claim", new ClaimActionInput(id), cancellationToken);

    public async Task<Result<WorkItemResponse>> AssignAsync(Guid id, AssignWorkRequest request, CancellationToken cancellationToken)
    {
        if (request?.StaffMemberId is not { } assignee)
        {
            return Error.Validation("staffMemberId", "work.assignee_required", "Say who should do the work.");
        }

        return await ActAsync(id, "HumanAction.Assign", new AssignActionInput(id, assignee), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// New work for an exception's request, as the failed work described it, with its time allowed starting now. It is
    /// keyed by the work it replaces, so asking twice asks once (P8).
    /// </summary>
    public async Task<Result<WorkItemResponse>> RequestAgainAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken).ConfigureAwait(false) is not { } failed)
        {
            return NotFound;
        }

        if (!await IsExceptionAsync(failed, cancellationToken).ConfigureAwait(false))
        {
            return NotAnException;
        }

        HumanActionRequest details = failed.Details;
        CreateActionInput input = new(
            failed.PropertyId,
            EnumMirror.To<ActionType>(details.ActionType),
            details.Instructions,
            details.Location,
            details.Reason,
            EnumMirror.To<ActionPriority>(details.Priority),
            details.Sla is { } sla ? (int)Math.Ceiling(sla.TotalMinutes) : null,
            Items: ValueList.From((details.Items ?? []).Select(item => new ActionItemEntry(item.Name, item.Quantity, item.Code))),
            SkillsRequired: details.SkillsRequired,
            ServiceRequestId: details.ServiceRequestId,
            SourceEventId: details.SourceEventId);
        Result<CapabilityResponse> created = await RunAsync(failed, "HumanAction.Create", input, $"request-again:{failed.Id.Value}", cancellationToken).ConfigureAwait(false);

        if (created.IsFailure)
        {
            return created.Error;
        }

        Guid newId = created.Value.Output.Deserialize<HumanActionOutput>(ContractJson.Options)!.Action.Source.Id;

        return await ReadBackAsync(newId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<WorkItemResponse>> ActAsync(Guid id, string capability, object input, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken).ConfigureAwait(false) is not { } action)
        {
            return NotFound;
        }

        Result<CapabilityResponse> done = await RunAsync(action, capability, input, null, cancellationToken).ConfigureAwait(false);

        return done.IsFailure ? done.Error : await ReadBackAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The work as it now stands, read in a scope of its own for the same tenant and caller.
    /// </summary>
    private async Task<Result<WorkItemResponse>> ReadBackAsync(Guid id, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(currentTenant.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(operation.Actor);

        return await scope.ServiceProvider.GetRequiredService<StaffWork>().GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private Task<Result<CapabilityResponse>> RunAsync(HumanAction action, string capability, object input, string? idempotencyKey, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            new CapabilityRequest(
                capability,
                JsonSerializer.SerializeToElement(input, ContractJson.Options),
                1,
                idempotencyKey,
                ContinuesFlow: action.Details.CorrelationId),
            cancellationToken);

    /// <summary>
    /// One piece of work, if the caller may see it.
    /// </summary>
    public async Task<Result<WorkItemResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken).ConfigureAwait(false) is not { } action)
        {
            return NotFound;
        }

        Result<Caller> caller = await CallerAsync(action.PropertyId, cancellationToken).ConfigureAwait(false);

        if (caller.IsFailure)
        {
            return caller.Error;
        }

        // Work a worker may not see is no work of theirs: neither theirs nor offered to them.
        if (!caller.Value.Sees(action) && !caller.Value.MayClaim(action))
        {
            return NotFound;
        }

        return Item(action, caller.Value, await IsExceptionAsync(action, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The latest work for each of the property's requests in exception, when that work failed, expired, or was cancelled.
    /// </summary>
    private async Task<IReadOnlyList<HumanAction>> ExceptionsAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        List<HumanAction> found = [];

        foreach (ServiceRequest request in await requests.ListInExceptionForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false))
        {
            IReadOnlyList<HumanAction> work = await actions.ListForServiceRequestAsync(request.Id, cancellationToken).ConfigureAwait(false);

            if (work.Count > 0 && IsUnsuccessful(work[^1]))
            {
                found.Add(work[^1]);
            }
        }

        return [.. found.OrderByDescending(action => action.EndedAt)];
    }

    private async Task<bool> IsExceptionAsync(HumanAction action, CancellationToken cancellationToken)
    {
        if (!IsUnsuccessful(action) || action.Details.ServiceRequestId is not { } requestId
            || await requests.GetByIdAsync(requestId, cancellationToken).ConfigureAwait(false) is not { Status: ServiceRequestStatus.Exception })
        {
            return false;
        }

        IReadOnlyList<HumanAction> work = await actions.ListForServiceRequestAsync(requestId, cancellationToken).ConfigureAwait(false);

        return work.Count > 0 && work[^1].Id == action.Id;
    }

    private static bool IsUnsuccessful(HumanAction action) =>
        action.Status is HumanActionStatus.Failed or HumanActionStatus.Expired or HumanActionStatus.Cancelled;

    private async Task<HumanAction?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? null : await actions.GetByIdAsync(new HumanActionId(id), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Who is asking, for <paramref name="propertyId"/>: managers and administrators oversee it; anyone else must be on its
    /// roster.
    /// </summary>
    private async Task<Result<Caller>> CallerAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        Actor actor = operation.Actor;
        StaffMemberId? id = actor.Type == ActorType.Staff && Guid.TryParse(actor.Id, out Guid value) && value != Guid.Empty ? new StaffMemberId(value) : null;
        StaffMember? member = id is { } staffMemberId ? await staff.GetByIdAsync(staffMemberId, cancellationToken).ConfigureAwait(false) : null;
        bool oversees = actor.Role is StaffRole.Manager or StaffRole.Admin;
        IReadOnlyList<StaffMember> roster = await staff.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        if (!oversees && member?.PropertyId != propertyId)
        {
            return NotOnRoster;
        }

        return new Caller(member?.PropertyId == propertyId ? member : null, oversees, actor.Role == StaffRole.Worker, roster.ToDictionary(person => person.Id));
    }

    private static WorkItemResponse Item(HumanAction action, Caller caller, bool isException)
    {
        HumanActionRequest details = action.Details;

        return new WorkItemResponse(
            action.Id.Value,
            action.PropertyId,
            EnumMirror.To<ActionType>(details.ActionType),
            EnumMirror.To<ActionStatus>(action.Status),
            details.Instructions,
            details.Location.Label,
            details.Reason,
            EnumMirror.To<ActionPriority>(details.Priority),
            ValueList.From((details.Items ?? []).Select(item => new ActionItemEntry(item.Name, item.Quantity, item.Code))),
            details.SkillsRequired ?? [],
            action.CreatedAt,
            action.Deadline,
            action.AssignedTo,
            action.AssignedTo is { } assignee ? caller.Roster.GetValueOrDefault(assignee)?.Details.DisplayName : null,
            action.Completion?.CompletedAt ?? action.EndedAt,
            action.Completion?.Note,
            action.FailureReason,
            ValueList.From(caller.ActionsOn(action, isException)));
    }

    /// <summary>
    /// The caller at one property.
    /// </summary>
    /// <param name="Member">Their roster entry there, if they are on it.</param>
    /// <param name="Oversees">Whether they are a manager or administrator.</param>
    /// <param name="IsWorker">Whether their role is worker, who alone may claim work.</param>
    /// <param name="Roster">The property's staff, for names.</param>
    private sealed record Caller(StaffMember? Member, bool Oversees, bool IsWorker, IReadOnlyDictionary<StaffMemberId, StaffMember> Roster)
    {
        public bool Sees(HumanAction action) => Oversees || (Member is { } member && action.AssignedTo == member.Id);

        public bool MayClaim(HumanAction action) =>
            IsWorker && Member is { } member && action.Status == HumanActionStatus.Requested && HumanActionRouting.IsEligible(member, action);

        public IEnumerable<WorkAction> ActionsOn(HumanAction action, bool isException)
        {
            if (Member is { } member && action.AssignedTo == member.Id)
            {
                switch (action.Status)
                {
                    case HumanActionStatus.Assigned:
                        yield return WorkAction.Accept;
                        break;
                    case HumanActionStatus.Accepted:
                        yield return WorkAction.Start;
                        yield return WorkAction.Fail;
                        break;
                    case HumanActionStatus.InProgress:
                        yield return WorkAction.Complete;
                        yield return WorkAction.Fail;
                        break;
                }
            }

            if (MayClaim(action))
            {
                yield return WorkAction.Claim;
            }

            if (Oversees && action.Status is HumanActionStatus.Requested or HumanActionStatus.Assigned)
            {
                yield return WorkAction.Assign;
            }

            if (Oversees && isException)
            {
                yield return WorkAction.RequestAgain;
            }
        }
    }
}
