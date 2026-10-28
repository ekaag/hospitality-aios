using Microsoft.Extensions.Options;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// Marker options type whose validation runs once at host startup and checks the embedded prompts.
/// </summary>
internal sealed class PromptStartupCheck
{
}

/// <summary>
/// Fails startup when an embedded prompt is unlocked, has changed since it was locked, is misnamed, or is numbered with
/// gaps, or when a lock file is malformed or locks a missing version (see <see cref="PromptRegistry"/>).
/// </summary>
internal sealed class PromptStartupValidator(PromptRegistry registry) : IValidateOptions<PromptStartupCheck>
{
    public ValidateOptionsResult Validate(string? name, PromptStartupCheck options) =>
        registry.Errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(registry.Errors);
}
