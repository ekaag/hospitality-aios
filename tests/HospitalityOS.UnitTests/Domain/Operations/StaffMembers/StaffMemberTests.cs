using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Domain.Operations.StaffMembers.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Operations.StaffMembers;

public sealed class StaffMemberTests
{
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209915001"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209915002"));
    private static readonly StaffMemberId sam = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209915003"));
    private static readonly DateTimeOffset now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesNameAndSkills_AndRaisesCreated()
    {
        StaffMember member = StaffMember.Create(sam, tenantId, propertyId, new StaffMemberDetails("  Sam Patel ", [" Minibar", "housekeeping", "minibar"]), true, now).Value;

        StaffMemberDetails normalized = new("Sam Patel", ["housekeeping", "minibar"]);
        Assert.Equal((normalized, true), (member.Details, member.IsOnDuty));
        Assert.Equal([new StaffMemberCreated(sam, propertyId, normalized, true, now)], member.DomainEvents);
    }

    [Fact]
    public void Create_WithoutANameOrWithAnInvalidSkill_ReportsBoth()
    {
        Result<StaffMember> result = StaffMember.Create(sam, tenantId, propertyId, new StaffMemberDetails(" ", ["house keeping"]), false, now);

        Assert.Equal(
            [("displayName", "staff_member.display_name_required"), ("skills[0]", "staff_member.skill_invalid")],
            Assert.IsType<ValidationError>(result.Error).Fields.Select(field => (field.Field, field.Code)));
    }

    [Fact]
    public void SetOnDuty_RaisesDutyChanged_AndTheCurrentDutyIsANoOp()
    {
        StaffMember member = StaffMember.Create(sam, tenantId, propertyId, new StaffMemberDetails("Sam Patel", ["housekeeping"]), false, now).Value;
        member.DequeueDomainEvents();

        Assert.True(member.SetOnDuty(false, now).IsSuccess);
        Assert.True(member.SetOnDuty(true, now).IsSuccess);
        Assert.True(member.SetOnDuty(true, now).IsSuccess);

        Assert.True(member.IsOnDuty);
        Assert.Equal([new StaffMemberDutyChanged(sam, propertyId, true, now)], member.DomainEvents);
    }

    [Fact]
    public void Update_RaisesUpdated_UnlessNothingChanged()
    {
        StaffMember member = StaffMember.Create(sam, tenantId, propertyId, new StaffMemberDetails("Sam Patel", ["housekeeping"]), false, now).Value;
        member.DequeueDomainEvents();

        member.Update(new StaffMemberDetails("Sam Patel", ["HOUSEKEEPING"]), now);
        member.Update(new StaffMemberDetails("Sam Patel", ["housekeeping", "minibar"]), now);

        Assert.Equal([new StaffMemberUpdated(sam, new StaffMemberDetails("Sam Patel", ["housekeeping", "minibar"]), now)], member.DomainEvents);
    }

    [Fact]
    public void HasSkills_NeedsEveryOne_AndAnyoneHasNone()
    {
        StaffMember member = StaffMember.Create(sam, tenantId, propertyId, new StaffMemberDetails("Sam Patel", ["housekeeping", "minibar"]), true, now).Value;

        Assert.Equal((true, true, false), (member.HasSkills([]), member.HasSkills(["housekeeping", "minibar"]), member.HasSkills(["housekeeping", "hvac"])));
    }
}
