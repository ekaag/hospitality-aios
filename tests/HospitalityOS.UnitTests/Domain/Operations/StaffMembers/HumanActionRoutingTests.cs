using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Operations.StaffMembers;

public sealed class HumanActionRoutingTests
{
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209916001"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209916002"));
    private static readonly PropertyId elsewhere = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209916003"));
    private static readonly DateTimeOffset now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly StaffMember jo = Member(10, "Jo Okafor", ["housekeeping"]);
    private static readonly StaffMember sam = Member(11, "Sam Patel", ["housekeeping", "minibar"]);
    private static readonly StaffMember ravi = Member(12, "Ravi Mehta", ["maintenance"]);

    [Fact]
    public void Choose_TakesSomeoneOnDutyWithEverySkill_ByNameOnATie()
    {
        Assert.Equal(jo, HumanActionRouting.Choose(Work(1, "housekeeping"), [sam, ravi, jo], []));
        Assert.Equal(sam, HumanActionRouting.Choose(Work(1, "housekeeping", "minibar"), [sam, ravi, jo], []));
        Assert.Equal(jo, HumanActionRouting.Choose(Work(1), [sam, ravi, jo], []));
    }

    [Fact]
    public void Choose_TakesWhoeverHoldsTheLeastOpenWork_CountingAcceptedButNotFinishedWork()
    {
        HumanAction josWork = Work(2, "housekeeping");
        josWork.Assign(jo.Id, "router", now);
        HumanAction josFinished = Work(3, "housekeeping");
        josFinished.Assign(jo.Id, "router", now);
        josFinished.Accept(jo.Id, now);
        josFinished.Complete(jo.Id, null, null, now);
        HumanAction samsFinished = Work(4, "housekeeping");
        samsFinished.Assign(sam.Id, "router", now);
        samsFinished.Fail(sam.Id, "Could not access the room", now);

        Assert.Equal(sam, HumanActionRouting.Choose(Work(1, "housekeeping"), [jo, sam], [josWork, josFinished, samsFinished]));
        Assert.Equal(jo, HumanActionRouting.Choose(josWork, [jo, sam], [josWork]));
    }

    [Fact]
    public void Choose_LeavesOutWhoeverIsOffDutyAtAnotherPropertyOrExcluded_AndMayFindNobody()
    {
        StaffMember offDuty = Member(13, "Al Offduty", ["housekeeping"]);
        offDuty.SetOnDuty(false, now);
        StaffMember visiting = StaffMember.Create(new StaffMemberId(Id(14)), tenantId, elsewhere, new StaffMemberDetails("Ann Away", ["housekeeping"]), true, now).Value;

        Assert.Equal(sam, HumanActionRouting.Choose(Work(1, "housekeeping"), [offDuty, visiting, jo, sam], [], excluding: jo.Id));
        Assert.Null(HumanActionRouting.Choose(Work(1, "spa"), [offDuty, visiting, jo, sam, ravi], []));
    }

    private static StaffMember Member(int seed, string name, ValueList<string> skills) =>
        StaffMember.Create(new StaffMemberId(Id(seed)), tenantId, propertyId, new StaffMemberDetails(name, skills), true, now).Value;

    private static HumanAction Work(int seed, params string[] skills) =>
        HumanAction.Request(
            new HumanActionId(Id(100 + seed)),
            tenantId,
            propertyId,
            new HumanActionRequest(HumanActionType.CheckRoom, "Check the room", new ActionLocation("Room 412"), "Reported by the guest", "test", SkillsRequired: ValueList.From(skills)),
            now).Value;

    private static Guid Id(int seed) => Guid.Parse($"01890a5d-ac96-774b-bcce-{seed:D12}");
}
