using HospitalityOS.Bindings;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace HospitalityOS.UnitTests.Bindings.Inference.TestDoubles;

/// <summary>
/// A service provider with inference routing configured from in-memory settings. Adapters: <c>Test</c> serves chat and
/// classification; <c>ChatOnly</c> serves chat only. Tasks declared: <c>concierge.answer</c> (chat) and
/// <c>intent.classify</c> (classification), unless the caller passes its own declarations.
/// </summary>
internal static class InferenceTestHost
{
    public const string Answer = "concierge.answer";
    public const string Classify = "intent.classify";

    public static ServiceProvider Build(IDictionary<string, string?> settings, Action<IServiceCollection>? declare = null)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();

        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 30, 0, TimeSpan.Zero)));
        services.AddSingleton<ModelCalls>();
        services.AddScoped<TenantContext>();
        services.AddInference(configuration);
        services.AddSingleton<IAdapterFactory<IChatModel>>(new TestModelFactory<IChatModel>("Test"));
        services.AddSingleton<IAdapterFactory<IClassifier>>(new TestModelFactory<IClassifier>("Test"));
        services.AddSingleton<IAdapterFactory<IChatModel>>(new TestModelFactory<IChatModel>("ChatOnly"));

        if (declare is null)
        {
            services.AddInferenceTask<IChatModel>(Answer);
            services.AddInferenceTask<IClassifier>(Classify);
        }
        else
        {
            declare(services);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>
    /// Settings for a provider named <paramref name="name"/> using adapter <paramref name="adapter"/>, labelled with its
    /// name, with optional extra settings.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Provider(string name, string adapter = "Test", params (string Key, string Value)[] settings) =>
    [
        new($"Inference:Providers:{name}:Adapter", adapter),
        new($"Inference:Providers:{name}:Settings:Label", name),
        .. settings.Select(setting => new KeyValuePair<string, string?>($"Inference:Providers:{name}:Settings:{setting.Key}", setting.Value)),
    ];

    /// <summary>
    /// Settings routing <paramref name="task"/> (for <paramref name="tenant"/>, if given) to a provider and model, then
    /// to each <c>provider:model</c> fallback.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Route(string task, string provider, string model, string[]? fallback = null, string? tenant = null)
    {
        string path = tenant is null ? $"Inference:Tasks:{task}" : $"Inference:Tenants:{tenant}:Tasks:{task}";

        return
        [
            new($"{path}:Provider", provider),
            new($"{path}:Model", model),
            .. (fallback ?? []).Select((target, index) => new KeyValuePair<string, string?>($"{path}:Fallback:{index}", target)),
        ];
    }

    /// <summary>
    /// The parts merged; a later part's value replaces an earlier one's, as later configuration sources do.
    /// </summary>
    public static Dictionary<string, string?> Settings(params IEnumerable<KeyValuePair<string, string?>>[] parts)
    {
        Dictionary<string, string?> merged = [];

        foreach (KeyValuePair<string, string?> setting in parts.SelectMany(part => part))
        {
            merged[setting.Key] = setting.Value;
        }

        return merged;
    }
}
