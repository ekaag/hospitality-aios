using HospitalityOS.SharedKernel.Execution;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Who sees AI traces unredacted (section <see cref="SectionName"/>). Traces are stored whole; every other reader gets them
/// redacted when read: the input and output of capabilities classified <c>Sensitive</c> are withheld, and email addresses,
/// phone numbers, and payment card numbers are masked in every text and JSON value.
/// </summary>
public sealed class AITraceRedactionOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Ai:Traces:Redaction";

    /// <summary>
    /// The staff roles that read traces unredacted, comma-separated <see cref="StaffRole"/> names; empty means nobody.
    /// Startup fails on an unknown role. Default: <c>Admin</c>.
    /// </summary>
    public string UnredactedRoles { get; set; } = nameof(StaffRole.Admin);

    /// <summary>
    /// Whether a reader in <paramref name="role"/> reads traces unredacted.
    /// </summary>
    public bool Reveals(StaffRole? role) => role is { } staff && Roles().Contains(staff.ToString(), StringComparer.Ordinal);

    /// <summary>
    /// Why <see cref="UnredactedRoles"/> is invalid, if it is.
    /// </summary>
    internal IEnumerable<string> Validate() =>
        Roles()
            .Where(role => !Enum.GetNames<StaffRole>().Contains(role, StringComparer.Ordinal))
            .Select(role => $"{SectionName}:{nameof(UnredactedRoles)} names '{role}', which is not a staff role ({string.Join(", ", Enum.GetNames<StaffRole>())}).");

    private string[] Roles() =>
        (UnredactedRoles ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
/// Fails startup when <see cref="AITraceRedactionOptions.UnredactedRoles"/> names an unknown role.
/// </summary>
internal sealed class AITraceRedactionValidator : IValidateOptions<AITraceRedactionOptions>
{
    public ValidateOptionsResult Validate(string? name, AITraceRedactionOptions options)
    {
        string[] errors = [.. options.Validate()];

        return errors.Length == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
