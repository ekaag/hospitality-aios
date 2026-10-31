using HospitalityOS.Domain.Operations.StaffMembers.Events;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.SharedKernel.Validation;

namespace HospitalityOS.Domain.Operations.StaffMembers;

/// <summary>
/// Someone on a property's roster who does physical work (spec §9 HumanAction Router): their skills, and whether they are
/// on duty, decide which human actions the router may give them. Their ID is the one their staff token names, so the
/// work they are assigned is theirs to accept and complete.
/// </summary>
/// <remarks>
/// Changes that change nothing are successful no-ops raising no event (P8).
/// </remarks>
public sealed class StaffMember : AggregateRoot<StaffMemberId>, ITenantScoped, IPropertyScoped
{
    /// <summary>
    /// Most skills a staff member may have.
    /// </summary>
    public const int MaxSkills = 20;

    // For EF Core only: it sets every property from storage before the instance is used, so the
    // placeholders below are never read.
    private StaffMember()
    {
        Details = null!;
    }

    private StaffMember(StaffMemberId id, TenantId tenantId, PropertyId propertyId, StaffMemberDetails details, bool isOnDuty)
        : base(id)
    {
        TenantId = tenantId;
        PropertyId = propertyId;
        Details = details;
        IsOnDuty = isOnDuty;
    }

    /// <inheritdoc />
    public TenantId TenantId { get; }

    /// <summary>
    /// The property whose roster they are on.
    /// </summary>
    public PropertyId PropertyId { get; }

    /// <summary>
    /// Name and skills, normalized.
    /// </summary>
    public StaffMemberDetails Details { get; private set; }

    /// <summary>
    /// Whether they are working now, so work may be routed to them.
    /// </summary>
    public bool IsOnDuty { get; private set; }

    /// <summary>
    /// Adds someone to a property's roster and raises <see cref="StaffMemberCreated"/>.
    /// </summary>
    /// <param name="id">Their ID, the subject of their staff token.</param>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="propertyId">The property.</param>
    /// <param name="details">Name and skills; validated and normalized.</param>
    /// <param name="isOnDuty">Whether they start on duty.</param>
    /// <param name="now">Current time from the injected <see cref="TimeProvider"/>.</param>
    /// <returns>The staff member, or a <see cref="ValidationError"/> listing every problem.</returns>
    public static Result<StaffMember> Create(StaffMemberId id, TenantId tenantId, PropertyId propertyId, StaffMemberDetails details, bool isOnDuty, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        Result<StaffMemberDetails> valid = Validate(details);

        if (valid.IsFailure)
        {
            return valid.Error;
        }

        StaffMember member = new(id, tenantId, propertyId, valid.Value, isOnDuty);
        member.Raise(new StaffMemberCreated(id, propertyId, member.Details, isOnDuty, now));

        return member;
    }

    /// <summary>
    /// Replaces name and skills and raises <see cref="StaffMemberUpdated"/>; unchanged details are a no-op.
    /// </summary>
    /// <returns>Success, or a <see cref="ValidationError"/>.</returns>
    public Result Update(StaffMemberDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        Result<StaffMemberDetails> valid = Validate(details);

        if (valid.IsFailure)
        {
            return valid.Error;
        }

        if (valid.Value == Details)
        {
            return Result.Success();
        }

        Details = valid.Value;
        Raise(new StaffMemberUpdated(Id, Details, now));

        return Result.Success();
    }

    /// <summary>
    /// Puts them on or off duty and raises <see cref="StaffMemberDutyChanged"/>; their current duty is a no-op.
    /// </summary>
    public Result SetOnDuty(bool isOnDuty, DateTimeOffset now)
    {
        if (isOnDuty == IsOnDuty)
        {
            return Result.Success();
        }

        IsOnDuty = isOnDuty;
        Raise(new StaffMemberDutyChanged(Id, PropertyId, isOnDuty, now));

        return Result.Success();
    }

    /// <summary>
    /// Whether they have every one of <paramref name="skills"/>; anyone has none.
    /// </summary>
    public bool HasSkills(IEnumerable<string> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);

        return skills.All(skill => Details.Skills!.Contains(skill));
    }

    private static Result<StaffMemberDetails> Validate(StaffMemberDetails details)
    {
        List<FieldError> errors = [];

        string name = TextRules.Required(details.DisplayName, "displayName", "staff_member.display_name", errors);
        ValueList<string> skills = details.Skills ?? [];
        SortedSet<string> normalized = new(StringComparer.Ordinal);

        for (int i = 0; i < skills.Count; i++)
        {
            string skill = TextRules.Code(skills[i], $"skills[{i}]", "staff_member.skill", errors);

            if (skill.Length > 0)
            {
                normalized.Add(skill);
            }
        }

        if (normalized.Count > MaxSkills)
        {
            errors.Add(new FieldError("skills", "staff_member.too_many_skills", $"At most {MaxSkills} skills are allowed."));
        }

        return errors.Count > 0
            ? ValidationError.From(errors)
            : new StaffMemberDetails(name, ValueList.From(normalized));
    }
}
