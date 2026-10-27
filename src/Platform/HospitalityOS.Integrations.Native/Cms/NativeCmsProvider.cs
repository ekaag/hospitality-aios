using HospitalityOS.IntegrationContracts.Cms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Integrations.Native.Cms;

internal sealed class NativeCmsProvider(Func<TenantId, NativeContentScope> scopes) : IIntegrationProvider<ICmsConnector>
{
    public const string ProviderName = "NativeCms";

    public string Name => ProviderName;

    public ICmsConnector Create(ConnectorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new NativeCmsConnector(scopes, context.TenantId, context.PropertyId);
    }
}
