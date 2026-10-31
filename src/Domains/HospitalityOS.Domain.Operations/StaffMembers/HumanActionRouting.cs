using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.StaffMembers;

/// <summary>
/// The HumanAction Router's deterministic rule (spec §9; workforce optimization is deferred): work goes to someone on the
/// action's property's roster who is on duty and has every skill it requires, choosing whoever holds the least open work
/// (assigned, accepted, or in progress), then by name, then by ID, so the same state always picks the same person.
/// </summary>
public static class HumanActionRouting
{
    /// <summary>
    /// Who should do <paramref name="action"/>, or <see langword="null"/> when nobody eligible is on duty.
    /// </summary>
    /// <param name="action">The work.</param>
    /// <param name="roster">The property's staff.</param>
    /// <param name="open">The property's open work, which sets each person's load.</param>
    /// <param name="excluding">Someone not to choose, e.g. who let the work go unaccepted.</param>
    public static StaffMember? Choose(HumanAction action, IEnumerable<StaffMember> roster, IEnumerable<HumanAction> open, StaffMemberId? excluding = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(open);

        Dictionary<StaffMemberId, int> load = open
            .Where(work => work.Id != action.Id && work.AssignedTo is not null
                && work.Status is HumanActionStatus.Assigned or HumanActionStatus.Accepted or HumanActionStatus.InProgress)
            .GroupBy(work => work.AssignedTo!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        return roster
            .Where(member => member.Id != excluding && IsEligible(member, action))
            .OrderBy(member => load.GetValueOrDefault(member.Id))
            .ThenBy(member => member.Details.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.Id.Value)
            .FirstOrDefault();
    }

    /// <summary>
    /// Whether <paramref name="member"/> may do <paramref name="action"/>: on duty at its property with every skill it
    /// requires. The router chooses among them, and a worker may claim unassigned work only then (J2).
    /// </summary>
    public static bool IsEligible(StaffMember member, HumanAction action)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(action);

        return member.IsOnDuty && member.PropertyId == action.PropertyId && member.HasSkills(action.Details.SkillsRequired ?? []);
    }
}
