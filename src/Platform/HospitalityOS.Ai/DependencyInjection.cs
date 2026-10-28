using HospitalityOS.Ai.Grounding;
using HospitalityOS.Ai.Retrieval;
using HospitalityOS.Ai.StructuredOutput;
using HospitalityOS.Ai.ToolLoops;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Bindings;
using HospitalityOS.Contracts.Events.Properties;
using HospitalityOS.Eventing;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Ai;

/// <summary>
/// Registers the AI layer over inference routing and the capability executor: tool loops (P7), validated typed
/// extraction (spec §35), grounded answering (P4), and content indexing. The host must also call <c>AddInference</c>,
/// <c>AddCapabilities</c>, and <c>AddBuiltInCapabilities</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// How many rounds of tool calls a tool loop allows by default before the model must answer.
    /// </summary>
    public const int DefaultMaxToolRounds = ToolLoopRegistration.DefaultMaxToolRounds;

    /// <summary>
    /// Registers <see cref="IToolLoop"/>, <see cref="ITypedExtractor"/>, <see cref="IGroundedAnswerer"/>, and the AI trace
    /// recorder and reader (<see cref="IAITraceRecorder"/>, <see cref="IAITraceReader"/>; scoped), with startup validation of
    /// the declared tool loops, grounded tasks, and trace redaction. The host binds <see cref="AITraceRedactionOptions"/>.
    /// </summary>
    public static IServiceCollection AddAi(this IServiceCollection services)
    {
        services.TryAddSingleton<ToolLoopCatalog>();
        services.TryAddScoped<IToolLoop, ToolLoop>();
        services.TryAddScoped<ITypedExtractor, TypedExtractor>();
        services.TryAddScoped<IGroundedAnswerer, GroundedAnswerer>();
        services.TryAddScoped<AITraceRecorder>();
        services.TryAddScoped<IAITraceRecorder>(provider => provider.GetRequiredService<AITraceRecorder>());
        services.TryAddScoped<IAITraceReader, AITraceReader>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ToolLoopStartupCheck>, ToolLoopStartupValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<GroundingStartupCheck>, GroundingStartupValidator>());
        services.AddOptions<ToolLoopStartupCheck>().ValidateOnStart();
        services.AddOptions<GroundingStartupCheck>().ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AITraceRedactionOptions>, AITraceRedactionValidator>());
        services.AddOptions<AITraceRedactionOptions>();

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
    /// Declares a chat task answered by <see cref="IGroundedAnswerer"/>: a tool loop (see <see cref="AddToolLoop"/>) whose
    /// prompt has a <c>{{context}}</c> placeholder for the property profile and matching content the answerer reads first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="task">The chat task.</param>
    /// <param name="prompt">The task's prompt; it must use <c>{{context}}</c>.</param>
    /// <param name="tools">The allow-list for precise reads: <c>Area.Action@version</c> entries.</param>
    /// <param name="maxToolRounds">Rounds of tool calls allowed per reply, 1 to 20.</param>
    public static IServiceCollection AddGroundedAnswering(
        this IServiceCollection services,
        string task,
        string prompt,
        IEnumerable<string> tools,
        int maxToolRounds = DefaultMaxToolRounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        services.AddToolLoop(task, prompt, tools, maxToolRounds);
        services.AddSingleton(new GroundedAnsweringRegistration(task, prompt));

        return services;
    }

    /// <summary>
    /// Keeps content search current from content events (the host must also call <c>AddEventing</c>).
    /// </summary>
    public static IServiceCollection AddContentIndexing(this IServiceCollection services) =>
        services
            .AddEventHandler<ContentItemCreatedV1, ContentIndexingHandler>(ContentIndexingHandler.OnCreated)
            .AddEventHandler<ContentItemUpdatedV1, ContentIndexingHandler>(ContentIndexingHandler.OnUpdated);

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
