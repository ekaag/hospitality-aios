using HospitalityOS.Application.Administration;
using HospitalityOS.Bindings;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Integrations;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.IntegrationRuntime.Configuration;
using HospitalityOS.IntegrationRuntime.Health;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractKind = HospitalityOS.Contracts.Api.Integrations.ConnectorKind;
using DomainKind = HospitalityOS.Domain.Integrations.IntegrationConfigurations.ConnectorKind;
using DomainStatus = HospitalityOS.Domain.Integrations.IntegrationConfigurations.IntegrationStatus;

namespace HospitalityOS.Api.Integrations;

/// <summary>
/// What a property and the platform are connected to (spec §34 Integrations), read-only: each of the property's connectors
/// with its provider and health, the platform's port bindings, and the AI tasks' models, by name only (settings may name
/// endpoints or secrets, so they are never shown).
/// </summary>
internal sealed class PropertyIntegrations(
    IPropertyRepository properties,
    IIntegrationHealth health,
    IIntegrationConfigurator configurator,
    IBindingInventory bindings,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IntegrationsResponse>> GetAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        if (await properties.GetByIdAsync(propertyId, cancellationToken).ConfigureAwait(false) is null)
        {
            return AdministrationErrors.PropertyNotFound;
        }

        IReadOnlyList<IntegrationHealthReport> reports = await health.CheckAsync(propertyId, cancellationToken).ConfigureAwait(false);
        string tenant = currentTenant.TenantId.Value.ToString();

        return new IntegrationsResponse(
            ValueList.From(Enum.GetValues<DomainKind>().Select(kind => Connector(kind, reports.FirstOrDefault(report => report.Kind == kind)))),
            ValueList.From(bindings.Ports(tenant).Select(port => new PortBindingResponse(port.Port, port.Scope, port.Adapter))),
            ValueList.From(bindings.InferenceTasks(tenant).Select(task =>
                new InferenceTaskResponse(task.Task, task.Capability, task.Provider, task.Adapter, task.Model, ValueList.From(task.Fallbacks)))));
    }

    private ConnectorResponse Connector(DomainKind kind, IntegrationHealthReport? report)
    {
        ValueList<string> available = ValueList.From(configurator.ListProviders(kind));

        if (report is null)
        {
            return new ConnectorResponse(EnumMirror.To<ContractKind>(kind), null, ConnectorStatus.NotConfigured, null, null, available);
        }

        // A disabled connector is not checked.
        bool active = report.Status == DomainStatus.Active;

        return new ConnectorResponse(
            EnumMirror.To<ContractKind>(kind),
            report.ProviderName,
            active ? ConnectorStatus.Active : ConnectorStatus.Disabled,
            active ? report.Health.IsHealthy : null,
            active && !report.Health.IsHealthy ? report.Health.Description : null,
            available);
    }
}
