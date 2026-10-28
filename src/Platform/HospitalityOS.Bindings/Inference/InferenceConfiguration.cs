using System.Globalization;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// The <c>Inference</c> configuration section (implementation plan §2.2): named providers (an adapter and its settings),
/// each task's route (provider, model, parameters, fallbacks), and per-tenant task overrides. Read once; problems are
/// collected in <see cref="Errors"/> for startup validation instead of thrown.
/// </summary>
/// <example>
/// <code>
/// "Inference": {
///   "Providers": { "anthropic": { "Adapter": "Anthropic", "Settings": { "ApiKeySecret": "anthropic-api-key" } } },
///   "Tasks": { "concierge.answer": { "Provider": "anthropic", "Model": "claude-sonnet-5-5",
///                                    "Parameters": { "Effort": "Medium" }, "Fallback": [ "fake:scripted" ] } },
///   "Tenants": { "&lt;tenant id&gt;": { "Tasks": { "concierge.answer": { "Provider": "...", "Model": "..." } } } }
/// }
/// </code>
/// </example>
internal sealed class InferenceConfiguration
{
    public const string SectionName = "Inference";
    public const string ProvidersKey = "Providers";
    public const string TasksKey = "Tasks";
    public const string TenantsKey = "Tenants";
    public const string AdapterKey = "Adapter";
    public const string SettingsKey = "Settings";
    public const string ProviderKey = "Provider";
    public const string ModelKey = "Model";
    public const string ParametersKey = "Parameters";
    public const string FallbackKey = "Fallback";

    private readonly List<string> errors = [];

    private InferenceConfiguration(IConfigurationSection section)
    {
        Path = section.Path;
        Providers = section.GetSection(ProvidersKey).GetChildren().ToDictionary(
            provider => provider.Key,
            provider => new InferenceProvider(provider.Key, provider.Path, provider[AdapterKey] ?? "", provider.GetSection(SettingsKey)),
            StringComparer.OrdinalIgnoreCase);
        Tasks = ReadTasks(section.GetSection(TasksKey));
        Tenants = section.GetSection(TenantsKey).GetChildren().ToDictionary(
            tenant => tenant.Key,
            tenant => (IReadOnlyDictionary<string, TaskRoute>)ReadTasks(tenant.GetSection(TasksKey)),
            StringComparer.OrdinalIgnoreCase);
    }

    public string Path { get; }

    public IReadOnlyDictionary<string, InferenceProvider> Providers { get; }

    public IReadOnlyDictionary<string, TaskRoute> Tasks { get; }

    /// <summary>
    /// Task overrides by tenant ID (as configured).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, TaskRoute>> Tenants { get; }

    public IReadOnlyList<string> Errors => errors;

    public static InferenceConfiguration FromSection(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return new InferenceConfiguration(section);
    }

    /// <summary>
    /// The tenant's override of the task, else the task's default route.
    /// </summary>
    public TaskRoute? Select(string task, string? tenant) =>
        tenant is not null && Tenants.TryGetValue(tenant, out IReadOnlyDictionary<string, TaskRoute>? overrides) && overrides.TryGetValue(task, out TaskRoute? route)
            ? route
            : Tasks.GetValueOrDefault(task);

    /// <summary>
    /// Every route configured: defaults and tenant overrides.
    /// </summary>
    public IEnumerable<TaskRoute> AllRoutes() => Tasks.Values.Concat(Tenants.Values.SelectMany(tenant => tenant.Values));

    private Dictionary<string, TaskRoute> ReadTasks(IConfigurationSection tasks)
    {
        Dictionary<string, TaskRoute> routes = new(StringComparer.OrdinalIgnoreCase);

        foreach (IConfigurationSection task in tasks.GetChildren())
        {
            if (ReadRoute(task) is { } route)
            {
                routes[task.Key] = route;
            }
        }

        return routes;
    }

    private TaskRoute? ReadRoute(IConfigurationSection task)
    {
        string? provider = task[ProviderKey];
        string? model = task[ModelKey];

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model))
        {
            errors.Add($"'{task.Path}' must set '{ProviderKey}' and '{ModelKey}'.");

            return null;
        }

        List<RouteTarget> fallbacks = [];

        foreach (IConfigurationSection fallback in task.GetSection(FallbackKey).GetChildren())
        {
            // "provider:model"; the model may itself contain ':' (e.g. a local model tag), the provider name may not.
            string[] parts = (fallback.Value ?? "").Split(':', 2, StringSplitOptions.TrimEntries);

            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                errors.Add($"'{fallback.Path}' must be 'provider:model', not '{fallback.Value}'.");
                continue;
            }

            fallbacks.Add(new RouteTarget(parts[0], parts[1]));
        }

        return new TaskRoute(task.Key, task.Path, new RouteTarget(provider.Trim(), model.Trim()), ReadParameters(task.GetSection(ParametersKey)), fallbacks);
    }

    private InferenceParameters ReadParameters(IConfigurationSection section)
    {
        int? maxOutputTokens = null;
        InferenceEffort? effort = null;

        foreach (IConfigurationSection parameter in section.GetChildren())
        {
            switch (parameter.Key.ToUpperInvariant())
            {
                case "MAXOUTPUTTOKENS":
                    maxOutputTokens = ReadTokens(parameter);
                    break;
                case "EFFORT":
                    effort = ReadEffort(parameter);
                    break;
                default:
                    errors.Add($"'{parameter.Path}' is not an inference parameter; use MaxOutputTokens or Effort.");
                    break;
            }
        }

        return new InferenceParameters(maxOutputTokens, effort);
    }

    private InferenceEffort? ReadEffort(IConfigurationSection parameter)
    {
        if (Enum.TryParse(parameter.Value, ignoreCase: true, out InferenceEffort effort) && Enum.IsDefined(effort) && !int.TryParse(parameter.Value, out _))
        {
            return effort;
        }

        errors.Add($"'{parameter.Path}' must be one of {string.Join(", ", Enum.GetNames<InferenceEffort>())}, not '{parameter.Value}'.");

        return null;
    }

    private int? ReadTokens(IConfigurationSection parameter)
    {
        if (int.TryParse(parameter.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= 1)
        {
            return value;
        }

        errors.Add($"'{parameter.Path}' must be a whole number of at least 1, not '{parameter.Value}'.");

        return null;
    }
}
