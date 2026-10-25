using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Dining.Menus.Events;

/// <summary>
/// A menu or any of its items changed (spec §8). Carries the full new menu, not a diff.
/// </summary>
public sealed record MenuUpdated(MenuId MenuId, MenuDetails Details, DateTimeOffset OccurredAt) : IDomainEvent;
