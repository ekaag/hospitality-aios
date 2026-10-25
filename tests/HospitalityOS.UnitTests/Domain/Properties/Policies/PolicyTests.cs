using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Domain.Properties.Policies.Events;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Properties.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Properties.Policies;

public sealed class PolicyTests
{
    private static readonly PolicyId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209906001"));
    private static readonly SubjectReference gym = new(SubjectKind.Amenity, Guid.Parse("01890a5d-ac96-774b-bcce-b30209905001"));

    // Demo 2: "Can my 12-year-old use the gym?"
    private static readonly PolicyDetails gymAgePolicy = new(
        PolicyType.AgeRestriction,
        "Gym age policy",
        "Guests under 16 may use the gym only when accompanied by an adult.",
        AppliesTo: gym);

    [Fact]
    public void Create_StartsAsDraftAndRaisesCreated()
    {
        Policy policy = Policy.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, gymAgePolicy, AuroraGrand.Now).Value;

        Assert.Equal(PolicyStatus.Draft, policy.Status);
        Assert.Equal(gymAgePolicy, policy.Details);
        Assert.Equal([new PolicyCreated(id, AuroraGrand.PropertyId, gymAgePolicy, PolicyStatus.Draft, AuroraGrand.Now)], policy.DomainEvents);
    }

    [Fact]
    public void Create_WithInvalidFields_ReportsAll()
    {
        PolicyDetails invalid = new(
            (PolicyType)99,
            "",
            new string('x', Policy.MaxContentLength + 1),
            new DateOnly(2026, 12, 31),
            new DateOnly(2026, 1, 1),
            new SubjectReference(SubjectKind.Amenity, Guid.Empty));

        Result<Policy> result = Policy.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, invalid, AuroraGrand.Now);

        Assert.Equal(
            [
                ("title", "policy.title_required"),
                ("content", "policy.content_too_long"),
                ("type", "policy.type_invalid"),
                ("effectiveTo", "policy.effective_range_invalid"),
                ("appliesTo.id", "policy.applies_to_id_required"),
            ],
            FieldCodes(result));
    }

    [Fact]
    public void Activate_FromDraft_PutsInForceAndRaisesUpdated()
    {
        Policy policy = CreateDraft();

        Result result = policy.Activate(AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyStatus.Active, policy.Status);
        Assert.Equal([new PolicyUpdated(id, gymAgePolicy, PolicyStatus.Active, AuroraGrand.Now.AddHours(1))], policy.DomainEvents);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsNoOp()
    {
        Policy policy = CreateDraft();
        policy.Activate(AuroraGrand.Now);
        policy.DequeueDomainEvents();

        Result result = policy.Activate(AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(policy.DomainEvents);
    }

    [Fact]
    public void Retired_CannotBeReactivatedOrUpdated()
    {
        Policy policy = CreateDraft();
        policy.Retire(AuroraGrand.Now);
        policy.DequeueDomainEvents();

        Result activate = policy.Activate(AuroraGrand.Now.AddHours(1));
        Result update = policy.Update(gymAgePolicy with { Title = "New title" }, AuroraGrand.Now.AddHours(1));

        Assert.Equal(ErrorKind.InvalidState, activate.Error.Kind);
        Assert.Equal("policy.retired", update.Error.Code);
        Assert.Equal(PolicyStatus.Retired, policy.Status);
        Assert.Empty(policy.DomainEvents);
    }

    [Fact]
    public void Retire_Twice_IsNoOp()
    {
        Policy policy = CreateDraft();
        policy.Retire(AuroraGrand.Now);
        policy.DequeueDomainEvents();

        Result result = policy.Retire(AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(policy.DomainEvents);
    }

    [Fact]
    public void Update_WithChangedContent_RaisesUpdatedKeepingStatus()
    {
        Policy policy = CreateDraft();
        policy.Activate(AuroraGrand.Now);
        policy.DequeueDomainEvents();
        PolicyDetails stricter = gymAgePolicy with { Content = "Guests under 18 may not use the gym." };

        Result result = policy.Update(stricter, AuroraGrand.Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, policy.Version);
        Assert.Equal([new PolicyUpdated(id, stricter, PolicyStatus.Active, AuroraGrand.Now.AddHours(1))], policy.DomainEvents);
    }

    [Theory]
    [InlineData(PolicyStatus.Draft, "2026-06-01", false)]
    [InlineData(PolicyStatus.Active, "2025-12-31", false)]
    [InlineData(PolicyStatus.Active, "2026-01-01", true)]
    [InlineData(PolicyStatus.Active, "2026-12-31", true)]
    [InlineData(PolicyStatus.Active, "2027-01-01", false)]
    [InlineData(PolicyStatus.Retired, "2026-06-01", false)]
    public void IsInEffectOn_RequiresActiveAndWithinEffectiveDates(PolicyStatus status, string date, bool expected)
    {
        PolicyDetails dated = gymAgePolicy with { EffectiveFrom = new DateOnly(2026, 1, 1), EffectiveTo = new DateOnly(2026, 12, 31) };
        Policy policy = Policy.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, dated, AuroraGrand.Now).Value;

        if (status == PolicyStatus.Active)
        {
            policy.Activate(AuroraGrand.Now);
        }
        else if (status == PolicyStatus.Retired)
        {
            policy.Retire(AuroraGrand.Now);
        }

        Assert.Equal(expected, policy.IsInEffectOn(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static Policy CreateDraft()
    {
        Policy policy = Policy.Create(id, AuroraGrand.TenantId, AuroraGrand.PropertyId, gymAgePolicy, AuroraGrand.Now).Value;
        policy.DequeueDomainEvents();

        return policy;
    }

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
