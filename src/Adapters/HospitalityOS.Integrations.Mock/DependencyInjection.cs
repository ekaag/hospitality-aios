using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Integrations.Mock.Messaging;
using HospitalityOS.Integrations.Mock.Pms;
using HospitalityOS.Integrations.Mock.Pos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Integrations.Mock;

/// <summary>
/// Registers the mock providers (spec §10): <c>MockPms</c>, <c>MockPos</c>, and <c>MockMessaging</c>, for development,
/// demos, and tests. They stand in for vendor systems, never call out, and keep nothing across restarts. A property uses
/// one only when its integration configuration selects it.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the three mock providers as <see cref="IIntegrationProvider{TConnector}"/> singletons. Their data comes
    /// from <see cref="MockPmsOptions"/> and <see cref="MockPosOptions"/>; <see cref="MockMessagingOptions"/> lists
    /// recipients to reject.
    /// </summary>
    public static IServiceCollection AddMockIntegrations(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<MockPmsOptions>();
        services.AddOptions<MockPosOptions>();
        services.AddOptions<MockMessagingOptions>();
        services.TryAddSingleton<MockMessagingGateway>();
        services.AddSingleton<IIntegrationProvider<IPmsConnector>, MockPmsProvider>();
        services.AddSingleton<IIntegrationProvider<IPosConnector>, MockPosProvider>();
        services.AddSingleton<IIntegrationProvider<IMessagingConnector>, MockMessagingProvider>();

        return services;
    }
}
