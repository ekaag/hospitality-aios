namespace HospitalityOS.Contracts.Api.Integrations;

/// <summary>
/// What one of the platform's ports is bound to (P6): its adapter, never its settings.
/// </summary>
/// <param name="Port">The port, e.g. <c>EventTransport</c>.</param>
/// <param name="Scope">Where the binding applies: <c>Default</c>, <c>Operation:{name}</c>, <c>Tenant</c>, or
/// <c>Tenant operation:{name}</c>.</param>
/// <param name="Adapter">The adapter, e.g. <c>InMemory</c>.</param>
public sealed record PortBindingResponse(string Port, string Scope, string Adapter);
