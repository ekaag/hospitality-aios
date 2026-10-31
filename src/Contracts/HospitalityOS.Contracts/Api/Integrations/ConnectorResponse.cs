using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Integrations;

/// <summary>
/// One of a property's connectors: which provider serves it and whether it is healthy. Settings are never shown.
/// </summary>
/// <param name="Kind">Which connector.</param>
/// <param name="Provider">The chosen provider, e.g. <c>Mock</c>; null when not configured.</param>
/// <param name="Status">Whether it is in use.</param>
/// <param name="Healthy">Whether its last check passed; null when it is not checked (not configured or disabled).</param>
/// <param name="Problem">What the check found, when it failed.</param>
/// <param name="AvailableProviders">The providers this deployment has for the connector.</param>
public sealed record ConnectorResponse(
    ConnectorKind Kind,
    string? Provider,
    ConnectorStatus Status,
    bool? Healthy,
    string? Problem,
    ValueList<string> AvailableProviders);
