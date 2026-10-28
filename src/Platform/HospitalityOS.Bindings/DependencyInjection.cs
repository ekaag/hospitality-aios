using HospitalityOS.Bindings.Inference;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Bindings;

/// <summary>
/// Registers the binding mechanism (implementation plan §2.4).
/// </summary>
public static class DependencyInjection
{
    public const string HealthCheckName = "bindings";

    public const string InferenceHealthCheckName = "inference";

    /// <summary>
    /// Registers binding resolution, startup validation, and the <c>Bindings</c> configuration section.
    /// Call once from the host; then declare each port with <see cref="AddPortBinding{TPort}"/>.
    /// </summary>
    public static IServiceCollection AddBindings(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(new BindingRoot(configuration.GetSection(BindingRoot.SectionName)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BindingStartupCheck>, BindingStartupValidator>());
        services.AddOptions<BindingStartupCheck>().ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Declares a port that must be bound in configuration under <c>Bindings:{key}</c>, and registers
    /// <see cref="IBindingResolver{TPort}"/> for it. Adapters are registered separately as
    /// <see cref="IAdapterFactory{TPort}"/> singletons by their own projects.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="key">Configuration key; defaults to the port type name without its leading <c>I</c>.</param>
    public static IServiceCollection AddPortBinding<TPort>(this IServiceCollection services, string? key = null)
        where TPort : class
    {
        PortRegistration<TPort> registration = new(key ?? DefaultKeyFor(typeof(TPort)));

        services.AddSingleton(registration);
        services.AddSingleton<PortRegistration>(registration);
        services.AddSingleton<IBindingResolver<TPort>, BindingResolver<TPort>>();

        return services;
    }

    /// <summary>
    /// Adds a health check that resolves and probes every configured binding.
    /// </summary>
    public static IHealthChecksBuilder AddBindingHealthChecks(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<BindingsHealthCheck>(HealthCheckName);
    }

    /// <summary>
    /// Registers inference routing (implementation plan §2.2, P6): <see cref="IInferenceRouter"/> (scoped) over the
    /// <c>Inference</c> configuration section, for the six inference ports (<see cref="IChatModel"/>,
    /// <see cref="IStructuredExtractor"/>, <see cref="IClassifier"/>, <see cref="IEmbeddingModel"/>, <see cref="IReranker"/>,
    /// <see cref="IEvaluationJudge"/>), with startup validation. Declare each task with
    /// <see cref="AddInferenceTask{TPort}"/>; adapter projects register <see cref="IAdapterFactory{TPort}"/> singletons for
    /// the ports they serve. The host must register <c>TenantContext</c>.
    /// </summary>
    public static IServiceCollection AddInference(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new InferenceRoot(configuration.GetSection(InferenceConfiguration.SectionName)));
        services.TryAddSingleton<InferenceCatalog>();
        AddInferencePort<IChatModel>(services);
        AddInferencePort<IStructuredExtractor>(services);
        AddInferencePort<IClassifier>(services);
        AddInferencePort<IEmbeddingModel>(services);
        AddInferencePort<IReranker>(services);
        AddInferencePort<IEvaluationJudge>(services);
        services.TryAddScoped<IInferenceRouter, InferenceRouter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<InferenceStartupCheck>, InferenceStartupValidator>());
        services.AddOptions<InferenceStartupCheck>().ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Declares an inference task the code runs through <typeparamref name="TPort"/>, e.g.
    /// <c>AddInferenceTask&lt;IChatModel&gt;("concierge.answer")</c>. Startup then requires a route for it under
    /// <c>Inference:Tasks</c> to providers whose adapters serve <typeparamref name="TPort"/>.
    /// </summary>
    public static IServiceCollection AddInferenceTask<TPort>(this IServiceCollection services, string task)
        where TPort : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);

        services.AddSingleton(new InferenceTaskRegistration(task, typeof(TPort)));

        return services;
    }

    /// <summary>
    /// Adds a health check that creates and probes every provider a declared task may use.
    /// </summary>
    public static IHealthChecksBuilder AddInferenceHealthChecks(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<InferenceHealthCheck>(InferenceHealthCheckName);
    }

    private static void AddInferencePort<TPort>(IServiceCollection services)
        where TPort : class
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<InferencePortAdapters, InferencePortAdapters<TPort>>());
    }

    private static string DefaultKeyFor(Type portType)
    {
        string name = portType.Name;

        return name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? name[1..] : name;
    }
}
