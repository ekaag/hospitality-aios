using Microsoft.Extensions.Options;

namespace HospitalityOS.Ai.ToolLoops;

/// <summary>
/// Marker options type whose validation runs once at host startup and checks the declared tool loops.
/// </summary>
internal sealed class ToolLoopStartupCheck
{
}

/// <summary>
/// Fails startup when a tool loop is declared twice, or allows a capability that is not registered at the pinned
/// version, has side effects, or does not allow agents, or allows one capability twice (see <see cref="ToolLoopCatalog"/>).
/// </summary>
internal sealed class ToolLoopStartupValidator(ToolLoopCatalog catalog) : IValidateOptions<ToolLoopStartupCheck>
{
    public ValidateOptionsResult Validate(string? name, ToolLoopStartupCheck options) =>
        catalog.Errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(catalog.Errors);
}
