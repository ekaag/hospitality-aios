using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Integrations;

/// <summary>
/// Which model an AI task uses for the tenant (P6).
/// </summary>
/// <param name="Task">The task, e.g. <c>concierge.answer</c>.</param>
/// <param name="Capability">What it does, e.g. <c>ChatModel</c>.</param>
/// <param name="Provider">The configured provider; empty when the task has no route.</param>
/// <param name="Adapter">The provider's adapter, e.g. <c>Anthropic</c>.</param>
/// <param name="Model">The model.</param>
/// <param name="Fallbacks">Where it falls back to, as <c>provider/model</c>, in order.</param>
public sealed record InferenceTaskResponse(string Task, string Capability, string Provider, string Adapter, string Model, ValueList<string> Fallbacks);
