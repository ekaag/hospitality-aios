namespace HospitalityOS.IntegrationContracts.Cms;

/// <summary>
/// Reads guest-facing content (FAQs, directions, announcements) from a content management system (spec §10). Initial
/// implementation: <c>NativeCms</c> (E3), the platform's own content, so a property can later move to an external CMS
/// through configuration alone.
/// </summary>
/// <remarks>
/// An instance serves one property: obtain it from the integration runtime's per-property resolver (E2). Business code
/// never references an implementation (spec §10). IDs in results are the provider's own; the runtime maps them to
/// canonical IDs through <c>ExternalReference</c> (P3). Expected failures are returned as <see cref="IntegrationErrors"/>;
/// provider outages are thrown.
/// </remarks>
public interface ICmsConnector
{
    /// <summary>
    /// Lists every published content item, in no particular order. The result is complete: an item missing from it is no
    /// longer published.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The published items; empty when there are none.</returns>
    Task<IReadOnlyList<CmsContent>> ListPublishedContentAsync(CancellationToken cancellationToken);
}
