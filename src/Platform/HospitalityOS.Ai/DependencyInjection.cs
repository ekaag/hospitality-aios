using HospitalityOS.Ai.StructuredOutput;
using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Bindings;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Ai;

/// <summary>
/// Registers the AI layer over inference routing and the capability executor: tool loops (P7) and validated typed
/// extraction (spec §35). The host must also call <c>AddInference</c> and <c>AddCapabilities</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// How many rounds of tool calls a tool loop allows by default before the model must answer.
    /// </summary>
    public const int DefaultMaxToolRounds = ToolLoopRegistration.DefaultMaxToolRounds;

    /// <summary>
    /// Registers <see cref="IToolLoop"/> and <see cref="ITypedExtractor"/> (scoped), with startup validation of the
    /// declared tool loops.
    /// </summary>
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        services.TryAddSingleton<ToolLoopCatalog>();
        services.TryAddScoped<IToolLoop, ToolLoop>();
        services.TryAddScoped<ITypedExtractor, TypedExtractor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ToolLoopStartupCheck>, ToolLoopStartupValidator>());
        services.AddOptions<ToolLoopStartupCheck>().ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Declares a chat task run as a tool loop, e.g.
    /// <c>AddToolLoop("concierge.answer", "concierge.answer", ["Property.GetOperatingHours@1", "Property.GetPolicy@1"])</c>.
    /// The allow-list is the only set of capabilities its model can call, each at the pinned version and offered under
    /// its name with <c>.</c> replaced by <c>_</c>. Startup fails unless every entry is registered, read-only, and allows
    /// agents. The task is declared for <see cref="IChatModel"/> with <paramref name="prompt"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="task">The chat task.</param>
    /// <param name="prompt">The task's prompt, or <c>null</c> for none.</param>
    /// <param name="tools">The allow-list: <c>Area.Action@version</c> entries.</param>
    /// <param name="maxToolRounds">Rounds of tool calls allowed before the model must answer, 1 to 20.</param>
    /// <exception cref="ArgumentException">An entry is malformed, there are none, or the round limit is out of range.</exception>
    public static IServiceCollection AddToolLoop(
        this IServiceCollection services,
        string task,
        string? prompt,
        IEnumerable<string> tools,
        int maxToolRounds = DefaultMaxToolRounds)
    {
        services.AddSingleton(ToolLoopRegistration.Create(task, tools, maxToolRounds));
        services.AddInferenceTask<IChatModel>(task, prompt);

        return services.AddAi();
    }

    /// <summary>
    /// Declares an extraction task for <see cref="ITypedExtractor"/>, run through <see cref="IStructuredExtractor"/> with
    /// <paramref name="prompt"/> as its instructions.
    /// </summary>
    public static IServiceCollection AddExtraction(this IServiceCollection services, string task, string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        services.AddInferenceTask<IStructuredExtractor>(task, prompt);

        return services.AddAi();
    }
}
