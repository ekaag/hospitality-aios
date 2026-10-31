using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Integrations;

/// <summary>
/// <c>GET /api/v1/integrations</c> (spec §34 Integrations): what a property and the platform are connected to, read-only.
/// Providers, bindings, and models change through configuration, not this API.
/// </summary>
/// <param name="Connectors">The property's connectors, one per kind.</param>
/// <param name="Ports">The platform's port bindings.</param>
/// <param name="InferenceTasks">The AI tasks and their models.</param>
public sealed record IntegrationsResponse(
    ValueList<ConnectorResponse> Connectors,
    ValueList<PortBindingResponse> Ports,
    ValueList<InferenceTaskResponse> InferenceTasks);
