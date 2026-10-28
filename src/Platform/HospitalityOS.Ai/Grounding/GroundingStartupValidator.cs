using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Ai.Grounding;

/// <summary>
/// A chat task declared with <c>AddGroundedAnswering</c>, and its prompt.
/// </summary>
internal sealed record GroundedAnsweringRegistration(string Task, string Prompt);

/// <summary>
/// Marker options type whose validation runs once at host startup and checks grounded answering.
/// </summary>
internal sealed class GroundingStartupCheck
{
}

/// <summary>
/// Fails startup when a grounded task's prompt (latest version) has no <c>{{context}}</c> placeholder, so the model would
/// never see the context, or when a context capability is not registered.
/// </summary>
internal sealed class GroundingStartupValidator(
    IEnumerable<GroundedAnsweringRegistration> registrations,
    IPromptRegistry prompts,
    CapabilityRegistry capabilities) : IValidateOptions<GroundingStartupCheck>
{
    public ValidateOptionsResult Validate(string? name, GroundingStartupCheck options)
    {
        List<string> errors = [];
        GroundedAnsweringRegistration[] declared = [.. registrations];

        foreach (GroundedAnsweringRegistration registration in declared)
        {
            if (prompts.Find(registration.Prompt) is { } prompt && !prompt.Variables.Contains(GroundedAnswerer.ContextVariable, StringComparer.Ordinal))
            {
                errors.Add($"Grounded task '{registration.Task}' uses prompt '{prompt.Version}', which has no {{{{{GroundedAnswerer.ContextVariable}}}}} placeholder for the context.");
            }
        }

        if (declared.Length > 0)
        {
            foreach (string capability in new[] { GroundedAnswerer.ProfileCapability, GroundedAnswerer.ContentCapability })
            {
                if (capabilities.Find(capability, GroundedAnswerer.ContextCapabilityVersion) is null)
                {
                    errors.Add($"Grounded answering reads {capability}@{GroundedAnswerer.ContextCapabilityVersion} for context, which is not registered (AddBuiltInCapabilities).");
                }
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
