using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// A configured model provider: one adapter with its settings (credentials are referenced, never stored). One provider
/// can serve several inference ports, e.g. chat and classification.
/// </summary>
/// <param name="Name">Name tasks use, e.g. <c>anthropic</c>.</param>
/// <param name="Path">Configuration path, for messages.</param>
/// <param name="Adapter">The adapter, e.g. <c>Anthropic</c> or <c>Fake</c>.</param>
/// <param name="Settings">The adapter's settings section.</param>
internal sealed record InferenceProvider(string Name, string Path, string Adapter, IConfiguration Settings);

/// <summary>
/// A provider and the model to ask it for.
/// </summary>
internal sealed record RouteTarget(string Provider, string Model);

/// <summary>
/// Where a task runs: the primary target, then each fallback in order, all with the task's parameters and prompt version
/// (<paramref name="PromptVersion"/>, or the prompt's latest when <c>null</c>).
/// </summary>
internal sealed record TaskRoute(
    string Task,
    string Path,
    RouteTarget Primary,
    InferenceParameters Parameters,
    IReadOnlyList<RouteTarget> Fallbacks,
    int? PromptVersion = null)
{
    public IEnumerable<RouteTarget> Targets => [Primary, .. Fallbacks];
}

/// <summary>
/// A task the code uses, declared with <c>AddInferenceTask&lt;TPort&gt;</c>, so startup can check it is bound to providers
/// that serve <paramref name="Port"/> and that its <paramref name="Prompt"/> exists.
/// </summary>
internal sealed record InferenceTaskRegistration(string Name, Type Port, string? Prompt = null);
