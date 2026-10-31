namespace HospitalityOS.Bindings;

/// <summary>
/// What each port and inference task is bound to, for administrators to see (spec §34 Integrations): adapter, provider,
/// and model names only, never settings, which may hold endpoints or secret names. Bindings change through configuration
/// and a restart, not here. Singleton.
/// </summary>
public interface IBindingInventory
{
    /// <summary>
    /// Every registered port's bindings, by port then scope: its default, its per-operation bindings, and the tenant's own
    /// overrides; other tenants' overrides are left out.
    /// </summary>
    /// <param name="tenant">The caller's tenant, whose overrides are included.</param>
    IReadOnlyList<BoundPort> Ports(string tenant);

    /// <summary>
    /// Every declared inference task with the provider and model it uses for the tenant, by task.
    /// </summary>
    /// <param name="tenant">The caller's tenant, whose routes win over the defaults.</param>
    IReadOnlyList<BoundInferenceTask> InferenceTasks(string tenant);
}

/// <summary>
/// One binding of a port.
/// </summary>
/// <param name="Port">The port's key, e.g. <c>EventTransport</c>.</param>
/// <param name="Scope">Where it applies: <c>Default</c>, <c>Operation:{name}</c>, <c>Tenant</c>, or <c>Tenant operation:{name}</c>.</param>
/// <param name="Adapter">The adapter, e.g. <c>InMemory</c>.</param>
public sealed record BoundPort(string Port, string Scope, string Adapter);

/// <summary>
/// The route of one inference task.
/// </summary>
/// <param name="Task">The task, e.g. <c>concierge.answer</c>.</param>
/// <param name="Capability">What it does: the inference port's name, e.g. <c>ChatModel</c>.</param>
/// <param name="Provider">The configured provider, e.g. <c>anthropic</c>; empty when the task has no route.</param>
/// <param name="Adapter">The provider's adapter, e.g. <c>Anthropic</c>.</param>
/// <param name="Model">The model.</param>
/// <param name="Fallbacks">Where it falls back to, as <c>provider/model</c>, in order.</param>
public sealed record BoundInferenceTask(string Task, string Capability, string Provider, string Adapter, string Model, IReadOnlyList<string> Fallbacks);
