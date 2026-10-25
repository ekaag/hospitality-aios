using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Operations.HumanActions;

public sealed class HumanActionTests
{
    private const string Router = "HumanActionRouter";

    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209914001"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209914002"));
    private static readonly HumanActionId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209914003"));
    private static readonly ServiceRequestId serviceRequestId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209914004"));
    private static readonly StaffMemberId maria = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209914005"));
    private static readonly StaffMemberId sam = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209914006"));
    private static readonly Guid sourceEventId = Guid.Parse("01890a5d-ac96-774b-bcce-b30209914007");
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    // Demo 5: "Room 412 — Deliver 2 bath towels".
    private static readonly HumanActionRequest deliverTowels = new(
        HumanActionType.DeliverItems,
        "Deliver 2 bath towels",
        new ActionLocation("Room 412"),
        "Guest requested towels via concierge",
        "GuestServiceController",
        HumanActionPriority.Normal,
        Sla: TimeSpan.FromMinutes(20),
        Items: [new ActionItem("Bath towel", 2, "bath_towel")],
        SkillsRequired: ["housekeeping"],
        ServiceRequestId: serviceRequestId,
        SourceEventId: sourceEventId);

    [Fact]
    public void Request_CreatesRequestedActionWithDeadlineFromSla()
    {
        HumanAction action = HumanAction.Request(id, tenantId, propertyId, deliverTowels, now).Value;

        Assert.Equal(HumanActionStatus.Requested, action.Status);
        Assert.Equal(now.AddMinutes(20), action.Deadline);
        Assert.Null(action.AssignedTo);
        Assert.Equal(
            [new HumanActionRequested(id, propertyId, serviceRequestId, HumanActionType.DeliverItems, HumanActionPriority.Normal, new ActionLocation("Room 412"), now.AddMinutes(20), ["housekeeping"], now)],
            action.DomainEvents);
    }

    [Fact]
    public void Request_WithExplicitDeadline_UsesItOverSla()
    {
        HumanAction action = HumanAction.Request(id, tenantId, propertyId, deliverTowels with { Deadline = now.AddHours(2) }, now).Value;

        Assert.Equal(now.AddHours(2), action.Deadline);
    }

    [Fact]
    public void Request_WithInvalidFields_ReportsAll()
    {
        HumanActionRequest invalid = deliverTowels with
        {
            Instructions = "",
            Reason = " ",
            RequestedByAgent = "",
            Items = [],
            SkillsRequired = ["house keeping"],
            Sla = TimeSpan.Zero,
            Deadline = now.AddMinutes(-1),
        };

        Result<HumanAction> result = HumanAction.Request(id, tenantId, propertyId, invalid, now);

        Assert.Equal(
            [
                ("instructions", "human_action.instructions_required"),
                ("reason", "human_action.reason_required"),
                ("requestedByAgent", "human_action.requested_by_agent_required"),
                ("skillsRequired[0]", "human_action.skill_invalid"),
                ("items", "human_action.items_required"),
                ("sla", "human_action.sla_out_of_range"),
                ("deadline", "human_action.deadline_in_past"),
            ],
            FieldCodes(result));
    }

    [Fact]
    public void HappyPath_AssignAcceptStartComplete_RaisesEachEventAndRecordsCompletion()
    {
        HumanAction action = Requested();

        action.Assign(maria, Router, now.AddMinutes(1));
        action.Accept(maria, now.AddMinutes(2));
        action.Start(maria, now.AddMinutes(3));
        Result result = action.Complete(maria, " Left at the door ", ["evidence/412-towels.jpg"], now.AddMinutes(8));

        Assert.True(result.IsSuccess);
        Assert.Equal(HumanActionStatus.Completed, action.Status);
        Assert.Equal(now.AddMinutes(2), action.AcceptedAt);
        Assert.Equal(now.AddMinutes(3), action.StartedAt);
        Assert.Equal(new HumanActionCompletion(maria, now.AddMinutes(8), "Left at the door", ["evidence/412-towels.jpg"]), action.Completion);
        Assert.Equal(
            [
                new HumanActionAssigned(id, maria, Router, now.AddMinutes(1)),
                new HumanActionAccepted(id, maria, now.AddMinutes(2)),
                new HumanActionStarted(id, maria, serviceRequestId, now.AddMinutes(3)),
                new HumanActionCompleted(id, maria, serviceRequestId, now.AddMinutes(8)),
            ],
            action.DomainEvents);
        Assert.Equal(5, action.Version);
    }

    [Fact]
    public void Complete_Twice_IsIdempotent_AndKeepsFirstCompletion()
    {
        HumanAction action = InProgress();

        action.Complete(maria, "first", null, now.AddMinutes(8));
        Result second = action.Complete(maria, "second", null, now.AddMinutes(9));

        Assert.True(second.IsSuccess);
        Assert.Equal("first", action.Completion!.Note);
        Assert.Single(action.DomainEvents.OfType<HumanActionCompleted>());
    }

    [Fact]
    public void Complete_FromAccepted_WithoutStarting_SetsStartedAt()
    {
        HumanAction action = Requested();
        action.Assign(maria, Router, now);
        action.Accept(maria, now);

        action.Complete(maria, null, null, now.AddMinutes(8));

        Assert.Equal(HumanActionStatus.Completed, action.Status);
        Assert.Equal(now.AddMinutes(8), action.StartedAt);
    }

    [Fact]
    public void RepeatedTransitions_AreNoOps()
    {
        HumanAction action = Requested();

        action.Assign(maria, Router, now);
        action.Assign(maria, Router, now);
        action.Accept(maria, now);
        action.Accept(maria, now);
        action.Start(maria, now);
        action.Start(maria, now);

        Assert.Equal(3, action.DomainEvents.Count);
    }

    [Fact]
    public void Reassign_BeforeAcceptance_KeepsAssignmentHistory()
    {
        HumanAction action = Requested();

        action.Assign(maria, Router, now);
        action.Assign(sam, "manager:lee", now.AddMinutes(5));

        Assert.Equal(sam, action.AssignedTo);
        Assert.Equal([new TaskAssignment(maria, Router, now), new TaskAssignment(sam, "manager:lee", now.AddMinutes(5))], action.Assignments);
        Assert.Equal("human_action.not_assignee", action.Accept(maria, now).Error.Code);
    }

    [Fact]
    public void Reassign_AfterAcceptance_IsRejected()
    {
        HumanAction action = Requested();
        action.Assign(maria, Router, now);
        action.Accept(maria, now);

        Assert.Equal("human_action.transition_invalid", action.Assign(sam, Router, now).Error.Code);
    }

    [Fact]
    public void OnlyAssignee_CanAcceptStartCompleteOrFail()
    {
        HumanAction action = Requested();
        action.Assign(maria, Router, now);

        Assert.Equal("human_action.not_assignee", action.Accept(sam, now).Error.Code);
        action.Accept(maria, now);
        Assert.Equal("human_action.not_assignee", action.Start(sam, now).Error.Code);
        Assert.Equal("human_action.not_assignee", action.Complete(sam, null, null, now).Error.Code);
        Assert.Equal("human_action.not_assignee", action.Fail(sam, "no key", now).Error.Code);
    }

    [Fact]
    public void IllegalTransitions_AreRejectedWithInvalidState()
    {
        HumanAction requested = Requested();
        HumanAction assigned = Requested();
        assigned.Assign(maria, Router, now);

        Assert.Equal("human_action.not_assignee", requested.Accept(maria, now).Error.Code);
        Assert.Equal("human_action.transition_invalid", assigned.Start(maria, now).Error.Code);
        Assert.Equal("human_action.transition_invalid", assigned.Complete(maria, null, null, now).Error.Code);
        Assert.Equal(ErrorKind.InvalidState, assigned.Start(maria, now).Error.Kind);
    }

    [Fact]
    public void Fail_CannotAccessRoom_EndsActionWithReason()
    {
        HumanAction action = InProgress();

        Result result = action.Fail(maria, " Could not access the room ", now.AddMinutes(5));
        Result again = action.Fail(maria, "Again", now.AddMinutes(6));

        Assert.True(result.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.Equal(HumanActionStatus.Failed, action.Status);
        Assert.Equal("Could not access the room", action.FailureReason);
        Assert.Equal([new HumanActionFailed(id, maria, serviceRequestId, "Could not access the room", now.AddMinutes(5))], action.DomainEvents);
    }

    [Fact]
    public void Fail_WithoutReason_ReturnsValidationError()
    {
        HumanAction action = InProgress();

        Assert.Equal("human_action.reason_required", Assert.Single(Assert.IsType<ValidationError>(action.Fail(maria, "", now).Error).Fields).Code);
        Assert.Equal(HumanActionStatus.InProgress, action.Status);
    }

    [Fact]
    public void FinalStates_RejectEveryOtherTransition()
    {
        HumanAction completed = InProgress();
        completed.Complete(maria, null, null, now);
        HumanAction failed = InProgress();
        failed.Fail(maria, "blocked", now);

        Assert.Equal("human_action.transition_invalid", completed.Fail(maria, "late", now).Error.Code);
        Assert.Equal("human_action.transition_invalid", completed.Cancel("late", now).Error.Code);
        Assert.Equal("human_action.transition_invalid", completed.Assign(sam, Router, now).Error.Code);
        Assert.Equal("human_action.transition_invalid", failed.Complete(maria, null, null, now).Error.Code);
        Assert.Equal("human_action.transition_invalid", failed.Expire(now.AddDays(1)).Error.Code);
    }

    [Fact]
    public void Cancel_BeforeWorkStarts_Succeeds_ButNotOnceInProgress()
    {
        HumanAction requested = Requested();
        HumanAction inProgress = InProgress();

        Assert.True(requested.Cancel("Guest cancelled", now).IsSuccess);
        Assert.True(requested.Cancel("Guest cancelled", now).IsSuccess);
        Assert.Equal(HumanActionStatus.Cancelled, requested.Status);
        Assert.Single(requested.DomainEvents.OfType<HumanActionCancelled>());
        Assert.Equal("human_action.transition_invalid", inProgress.Cancel("Guest cancelled", now).Error.Code);
    }

    [Fact]
    public void Expire_AfterDeadline_Succeeds_ButNotBefore()
    {
        HumanAction action = Requested();

        Result early = action.Expire(now.AddMinutes(19));
        Result onTime = action.Expire(now.AddMinutes(20));

        Assert.Equal("human_action.deadline_not_passed", early.Error.Code);
        Assert.True(onTime.IsSuccess);
        Assert.Equal(HumanActionStatus.Expired, action.Status);
        Assert.Equal([new HumanActionExpired(id, serviceRequestId, now.AddMinutes(20))], action.DomainEvents);
    }

    [Fact]
    public void Expire_WithoutDeadline_IsRejected_AndInProgressWorkNeverExpires()
    {
        HumanAction noDeadline = HumanAction.Request(id, tenantId, propertyId, deliverTowels with { Sla = null }, now).Value;
        HumanAction inProgress = InProgress();

        Assert.Equal("human_action.deadline_not_passed", noDeadline.Expire(now.AddDays(1)).Error.Code);
        Assert.Equal("human_action.transition_invalid", inProgress.Expire(now.AddDays(1)).Error.Code);
    }

    private static HumanAction Requested()
    {
        HumanAction action = HumanAction.Request(id, tenantId, propertyId, deliverTowels, now).Value;
        action.DequeueDomainEvents();

        return action;
    }

    private static HumanAction InProgress()
    {
        HumanAction action = Requested();
        action.Assign(maria, Router, now);
        action.Accept(maria, now);
        action.Start(maria, now);
        action.DequeueDomainEvents();

        return action;
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
