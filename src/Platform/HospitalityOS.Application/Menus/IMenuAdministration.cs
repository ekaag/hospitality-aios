using HospitalityOS.Contracts.Api.Menus;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Menus;

/// <summary>
/// Creates, replaces, and reads restaurant menus in the caller's tenant (Journeys 3 and 7), and marks items sold out.
/// Commands commit their change and its audit record together; rejected commands are audited too. Scoped; the caller's
/// tenant and actor come from the operation.
/// </summary>
public interface IMenuAdministration
{
    /// <summary>
    /// Creates a menu for a restaurant and raises <c>MenuCreated</c>; every item gets a new ID.
    /// </summary>
    /// <returns>The menu, or a <see cref="ValidationError"/> listing every invalid field (a restaurant not in the tenant
    /// is the field <c>restaurantId</c>; item problems are reported by position, e.g. <c>items[2].name</c>).</returns>
    Task<Result<MenuResponse>> CreateAsync(CreateMenuRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a whole menu and raises <c>MenuUpdated</c>; an unchanged menu is a no-op. Items keep the ID they are sent
    /// with, which must be on the menu (<c>menu.item_not_found</c> on <c>items[i].id</c>); items without one are new.
    /// </summary>
    /// <param name="id">The menu.</param>
    /// <param name="request">The new menu.</param>
    /// <param name="expectedVersion">The version the caller read, when it wants the change refused if someone changed the
    /// menu since.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The menu; <c>menu.not_found</c>; <c>request.version_mismatch</c>; or a <see cref="ValidationError"/>.</returns>
    Task<Result<MenuResponse>> UpdateAsync(Guid id, UpdateMenuRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Marks one item available or sold out and raises <c>MenuUpdated</c>; setting the current value is a no-op.
    /// </summary>
    /// <param name="id">The menu.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="request">Whether it can be ordered.</param>
    /// <param name="expectedVersion">The version the caller read, if any.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The menu; <c>menu.not_found</c>; <c>menu.item_not_found</c>; <c>request.version_mismatch</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<MenuResponse>> SetItemAvailabilityAsync(Guid id, Guid itemId, SetItemAvailabilityRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a menu.
    /// </summary>
    /// <returns>The menu, or <c>menu.not_found</c>.</returns>
    Task<Result<MenuResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A restaurant's menus, by name; none when the restaurant is not in the caller's tenant.
    /// </summary>
    Task<IReadOnlyList<MenuResponse>> ListAsync(Guid restaurantId, CancellationToken cancellationToken);
}
