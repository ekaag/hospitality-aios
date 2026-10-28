using System.Text.Json;
using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Adapters.Local.Inference;

/// <summary>
/// Binds an inference port to <see cref="FakeModel"/> under the adapter name <c>Fake</c>. Settings: <c>Script</c> names a
/// script registered in <see cref="FakeInferenceOptions"/> (optional); <c>Unavailable</c> <c>true</c> makes every call fail
/// as an outage.
/// </summary>
internal sealed class FakeModelFactory<TPort>(IOptions<FakeInferenceOptions> options) : IAdapterFactory<TPort>
    where TPort : class
{
    public const string Name = "Fake";

    public string AdapterName => Name;

    public TPort Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        FakeScriptOptions? script = settings["Script"] is { } name ? options.Value.Scripts[name] : null;

        return (TPort)(object)new FakeModel(script, bool.TryParse(settings["Unavailable"], out bool unavailable) && unavailable);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings["Unavailable"] is { } unavailable && !bool.TryParse(unavailable, out _))
        {
            yield return $"Unavailable must be true or false, not '{unavailable}'.";
        }

        if (settings["Script"] is not { } name)
        {
            yield break;
        }

        if (!options.Value.Scripts.TryGetValue(name, out FakeScriptOptions? script))
        {
            string registered = options.Value.Scripts.Count == 0 ? "(none)" : string.Join(", ", options.Value.Scripts.Keys.Order(StringComparer.OrdinalIgnoreCase));
            yield return $"Script '{name}' is not registered. Registered: {registered}.";
            yield break;
        }

        foreach (string problem in ValidateScript(name, script))
        {
            yield return problem;
        }
    }

    private static IEnumerable<string> ValidateScript(string name, FakeScriptOptions script)
    {
        for (int i = 0; i < script.Chat.Count; i++)
        {
            FakeChatRuleOptions rule = script.Chat[i];

            if ((rule.Reply is null) == (rule.CallTool is null))
            {
                yield return $"Script '{name}' chat rule {i} must either reply or call a tool.";
            }

            if (rule.Arguments is { } arguments && !IsJson(arguments, JsonValueKind.Object))
            {
                yield return $"Script '{name}' chat rule {i} arguments must be a JSON object.";
            }
        }

        for (int i = 0; i < script.Extraction.Count; i++)
        {
            if (!IsJson(script.Extraction[i].Output, null))
            {
                yield return $"Script '{name}' extraction rule {i} output must be JSON.";
            }
        }
    }

    private static bool IsJson(string text, JsonValueKind? kind)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);

            return kind is null || document.RootElement.ValueKind == kind;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
