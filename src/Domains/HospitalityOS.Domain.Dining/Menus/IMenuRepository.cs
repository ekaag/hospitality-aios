namespace HospitalityOS.Domain.Dining.Menus;

/// <summary>
/// Persistence port for <see cref="Menu"/>. Implementations are scoped to the current tenant (P10) and commit
/// through the unit of work. Dietary search across menus goes through read-side queries (D3).
/// </summary>
public interface IMenuRepository
{
    /// <summary>
    /// Loads a menu, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<Menu?> GetByIdAsync(MenuId id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new menu, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(Menu menu, CancellationToken cancellationToken);
}
