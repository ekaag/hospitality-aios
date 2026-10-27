using System.Globalization;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Cms;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using ContractContentType = HospitalityOS.IntegrationContracts.Cms.ContentType;

namespace HospitalityOS.Integrations.Native.Cms;

/// <summary>
/// The platform's own CMS: a property's published content items. The provider ID is the canonical ID and the revision is
/// the item's version, so nothing needs mapping.
/// </summary>
internal sealed class NativeCmsConnector(Func<TenantId, NativeContentScope> scopes, TenantId tenantId, PropertyId propertyId) : ICmsConnector
{
    public async Task<IReadOnlyList<CmsContent>> ListPublishedContentAsync(CancellationToken cancellationToken)
    {
        await using NativeContentScope scope = scopes(tenantId);
        IReadOnlyList<ContentItem> published = await scope.ContentItems.ListPublishedForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. published.Select(item => new CmsContent(
            new ExternalId(item.Id.Value.ToString()),
            Enum.Parse<ContractContentType>(item.Details.Type.ToString()),
            item.Details.Title,
            item.Details.Body,
            item.Details.Tags ?? ValueList.From<string>([]),
            item.Version.ToString(CultureInfo.InvariantCulture)))];
    }
}
