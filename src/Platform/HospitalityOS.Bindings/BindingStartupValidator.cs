using Microsoft.Extensions.Options;

namespace HospitalityOS.Bindings;

/// <summary>
/// Fails startup when a required port has no default binding, a binding names an unknown
/// adapter or has invalid settings, or configuration contains bindings for unknown ports.
/// </summary>
internal sealed class BindingStartupValidator : IValidateOptions<BindingStartupCheck>
{
    private readonly IServiceProvider services;
    private readonly IEnumerable<PortRegistration> registrations;
    private readonly BindingRoot root;

    public BindingStartupValidator(IServiceProvider services, IEnumerable<PortRegistration> registrations, BindingRoot root)
    {
        this.services = services;
        this.registrations = registrations;
        this.root = root;
    }

    public ValidateOptionsResult Validate(string? name, BindingStartupCheck options)
    {
        List<string> errors = [.. FindUnknownPorts(), .. registrations.SelectMany(registration => registration.Validate(services))];

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private IEnumerable<string> FindUnknownPorts()
    {
        HashSet<string> knownKeys = registrations.Select(registration => registration.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return root.Section
            .GetChildren()
            .Where(section => !knownKeys.Contains(section.Key))
            .Select(section => $"'{section.Path}' configures an unknown port. Registered ports: {DescribeKnownKeys(knownKeys)}.");
    }

    private static string DescribeKnownKeys(HashSet<string> knownKeys)
    {
        return knownKeys.Count == 0 ? "(none)" : string.Join(", ", knownKeys.Order(StringComparer.OrdinalIgnoreCase));
    }
}
