using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationContracts.Cms;
using HospitalityOS.IntegrationContracts.Crs;
using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationContracts.Payments;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Pos;

namespace HospitalityOS.IntegrationRuntime.Providers;

/// <summary>
/// Which <see cref="ConnectorKind"/> each spec §10 connector interface is.
/// </summary>
internal static class ConnectorKinds
{
    private static readonly Dictionary<Type, ConnectorKind> byConnector = new()
    {
        [typeof(IPmsConnector)] = ConnectorKind.Pms,
        [typeof(ICmsConnector)] = ConnectorKind.Cms,
        [typeof(IPosConnector)] = ConnectorKind.Pos,
        [typeof(IMessagingConnector)] = ConnectorKind.Messaging,
        [typeof(ICrsConnector)] = ConnectorKind.Crs,
        [typeof(IPaymentConnector)] = ConnectorKind.Payment,
    };

    public static IReadOnlyDictionary<Type, ConnectorKind> All => byConnector;

    public static ConnectorKind Of<TConnector>()
        where TConnector : class =>
        byConnector.TryGetValue(typeof(TConnector), out ConnectorKind kind)
            ? kind
            : throw new InvalidOperationException($"{typeof(TConnector).Name} is not a connector interface from HospitalityOS.IntegrationContracts.");
}
