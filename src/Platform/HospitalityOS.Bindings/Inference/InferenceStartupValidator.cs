using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// Marker options type whose validation runs once at host startup and checks the inference configuration.
/// </summary>
internal sealed class InferenceStartupCheck
{
}

/// <summary>
/// Fails startup when the <c>Inference</c> section cannot be read; a declared task has no default route, or a route
/// names an unknown provider or one whose adapter does not serve the task's port; a provider's adapter is unknown or its
/// settings are invalid; a route is configured for a task the code does not declare (usually a typo); or a task's prompt
/// does not exist, or a route pins a version of it that does not exist.
/// </summary>
internal sealed class InferenceStartupValidator(InferenceCatalog catalog, IPromptRegistry prompts) : IValidateOptions<InferenceStartupCheck>
{
    public ValidateOptionsResult Validate(string? name, InferenceStartupCheck options)
    {
        List<string> errors = [.. catalog.Configuration.Errors, .. ValidateTasks(), .. ValidateRoutes(), .. ValidateProviders()];

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private IEnumerable<string> ValidateTasks()
    {
        foreach (IGrouping<string, InferenceTaskRegistration> declared in catalog.Tasks.GroupBy(task => task.Name, StringComparer.OrdinalIgnoreCase))
        {
            Type[] ports = [.. declared.Select(task => task.Port).Distinct()];

            if (ports.Length > 1)
            {
                yield return $"Inference task '{declared.Key}' is declared for more than one port: {string.Join(", ", ports.Select(port => port.Name))}.";
            }

            string?[] declaredPrompts = [.. declared.Select(task => task.Prompt).Distinct(StringComparer.OrdinalIgnoreCase)];

            if (declaredPrompts.Length > 1)
            {
                yield return $"Inference task '{declared.Key}' is declared with more than one prompt: {string.Join(", ", declaredPrompts.Select(prompt => prompt ?? "(none)"))}.";
            }
            else if (declaredPrompts[0] is { } prompt && prompts.Find(prompt) is null)
            {
                yield return $"Inference task '{declared.Key}' uses prompt '{prompt}', which is not embedded in any assembly registered with AddPrompts.";
            }

            if (catalog.AdaptersFor(ports[0]) is null)
            {
                yield return $"Inference task '{declared.Key}' is declared for {ports[0].Name}, which is not an inference port.";
            }

            if (!catalog.Configuration.Tasks.ContainsKey(declared.Key))
            {
                yield return $"Inference task '{declared.Key}' has no route. Add '{catalog.Configuration.Path}:{InferenceConfiguration.TasksKey}:{declared.Key}'.";
            }
        }
    }

    private IEnumerable<string> ValidateRoutes()
    {
        foreach (TaskRoute route in catalog.Configuration.AllRoutes())
        {
            if (catalog.FindTask(route.Task) is not { } task)
            {
                yield return $"'{route.Path}' routes inference task '{route.Task}', which the code does not declare.";
                continue;
            }

            if (route.PromptVersion is { } version)
            {
                if (task.Prompt is null)
                {
                    yield return $"'{route.Path}:{InferenceConfiguration.PromptVersionKey}' pins a prompt version, but inference task '{route.Task}' declares no prompt.";
                }
                else if (prompts.Find(task.Prompt) is { } latest && prompts.Find(task.Prompt, version) is null)
                {
                    yield return $"'{route.Path}:{InferenceConfiguration.PromptVersionKey}' pins prompt '{task.Prompt}@{version}', which does not exist (the latest is {latest.Version}).";
                }
            }

            foreach (RouteTarget target in route.Targets)
            {
                if (!catalog.Configuration.Providers.TryGetValue(target.Provider, out InferenceProvider? provider))
                {
                    yield return $"'{route.Path}' uses provider '{target.Provider}', which is not configured under '{catalog.Configuration.Path}:{InferenceConfiguration.ProvidersKey}'.";
                }
                else if (catalog.AdaptersFor(task.Port) is { } adapters && !adapters.Has(provider.Adapter))
                {
                    string registered = adapters.AdapterNames.Count == 0 ? "(none)" : string.Join(", ", adapters.AdapterNames.Order(StringComparer.OrdinalIgnoreCase));
                    yield return $"'{route.Path}' uses provider '{provider.Name}', whose adapter '{provider.Adapter}' does not serve {task.Port.Name}. Adapters serving it: {registered}.";
                }
            }
        }
    }

    private IEnumerable<string> ValidateProviders()
    {
        foreach (InferenceProvider provider in catalog.Configuration.Providers.Values)
        {
            if (string.IsNullOrWhiteSpace(provider.Adapter))
            {
                yield return $"'{provider.Path}' must set '{InferenceConfiguration.AdapterKey}'.";
                continue;
            }

            InferencePortAdapters[] serving = [.. catalog.Ports.Where(port => port.Has(provider.Adapter))];

            if (serving.Length == 0)
            {
                yield return $"'{provider.Path}' selects unknown inference adapter '{provider.Adapter}'.";
                continue;
            }

            // An adapter project registers one factory per port it serves; their settings rules are the same, so
            // duplicate messages are dropped.
            foreach (string error in serving.SelectMany(port => port.ValidateSettings(provider.Adapter, provider.Settings)).Distinct(StringComparer.Ordinal))
            {
                yield return $"'{provider.Path}:{InferenceConfiguration.SettingsKey}': {error}";
            }
        }
    }
}
