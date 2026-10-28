using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// A tool loop ready to run: the tools offered to the model and the capability each one runs as.
/// </summary>
/// <param name="Task">The chat task.</param>
/// <param name="MaxToolRounds">How many rounds of tool calls the model may make before it must answer.</param>
/// <param name="Tools">What the model is offered.</param>
/// <param name="Capabilities">The capability behind each tool, by tool name (exact).</param>
internal sealed record ToolLoopDefinition(
    string Task,
    int MaxToolRounds,
    ValueList<ChatTool> Tools,
    IReadOnlyDictionary<string, CapabilityDefinition> Capabilities);

/// <summary>
/// Every declared tool loop, resolved against the capability registry once. Phase 1 agents may call only read-only
/// capabilities that allow the <see cref="CapabilityCaller.Agent"/> caller: writes are made by controllers, which
/// supply idempotency keys and confirmation themselves. Problems are collected in <see cref="Errors"/> for startup
/// validation. Singleton.
/// </summary>
internal sealed class ToolLoopCatalog
{
    private readonly Dictionary<string, ToolLoopDefinition> loops = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> errors = [];

    public ToolLoopCatalog(IEnumerable<ToolLoopRegistration> registrations, CapabilityRegistry capabilities)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(capabilities);

        foreach (ToolLoopRegistration registration in registrations)
        {
            if (loops.ContainsKey(registration.Task))
            {
                errors.Add($"Tool loop '{registration.Task}' is declared more than once.");
                continue;
            }

            loops.Add(registration.Task, Resolve(registration, capabilities));
        }
    }

    public IReadOnlyList<string> Errors => errors;

    public ToolLoopDefinition? Find(string task) => loops.GetValueOrDefault(task);

    /// <summary>
    /// The tool name a model sees for a capability: providers allow only letters, digits, <c>_</c>, and <c>-</c>.
    /// </summary>
    internal static string ToolName(string capability) => capability.Replace('.', '_');

    private ToolLoopDefinition Resolve(ToolLoopRegistration registration, CapabilityRegistry capabilities)
    {
        List<ChatTool> tools = [];
        Dictionary<string, CapabilityDefinition> byTool = new(StringComparer.Ordinal);

        foreach ((string name, int version) in registration.Tools)
        {
            string entry = $"{name}@{version}";

            if (byTool.ContainsKey(ToolName(name)))
            {
                errors.Add($"Tool loop '{registration.Task}' allows {name} more than once; allow one version.");
                continue;
            }

            if (capabilities.Find(name, version) is not { } capability)
            {
                errors.Add($"Tool loop '{registration.Task}' allows {entry}, which is not registered.");
                continue;
            }

            if (capability.SideEffects is not SideEffect.None)
            {
                errors.Add($"Tool loop '{registration.Task}' allows {entry}, which has {capability.SideEffects} side effects; agents may call read-only capabilities only.");
                continue;
            }

            if (!capability.Authorization.AllowedCallers.Contains(CapabilityCaller.Agent))
            {
                errors.Add($"Tool loop '{registration.Task}' allows {entry}, which agents may not invoke.");
                continue;
            }

            tools.Add(new ChatTool(ToolName(name), capability.Description, capability.InputSchema));
            byTool.Add(ToolName(name), capability);
        }

        return new ToolLoopDefinition(registration.Task, registration.MaxToolRounds, ValueList.From(tools), byTool);
    }
}
