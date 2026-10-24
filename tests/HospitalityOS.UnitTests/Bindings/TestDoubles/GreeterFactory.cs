using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.UnitTests.Bindings.TestDoubles;

/// <summary>
/// Sample adapter factory. Settings: <c>Punctuation</c> (optional, single character),
/// <c>Healthy</c> (optional, default <c>true</c>).
/// </summary>
internal sealed class GreeterFactory : IAdapterFactory<IGreeter>
{
    private readonly string salutation;

    public GreeterFactory(string adapterName, string salutation)
    {
        AdapterName = adapterName;
        this.salutation = salutation;
    }

    public string AdapterName { get; }

    public int CreatedCount { get; private set; }

    public static GreeterFactory English() => new("English", "Hello");

    public static GreeterFactory French() => new("French", "Bonjour");

    public IGreeter Create(IServiceProvider services, IConfiguration settings)
    {
        CreatedCount++;

        return new Greeter(
            salutation,
            settings["Punctuation"] ?? ".",
            !string.Equals(settings["Healthy"], "false", StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        string? punctuation = settings["Punctuation"];

        if (punctuation is not null && punctuation.Length != 1)
        {
            yield return "Punctuation must be a single character.";
        }
    }
}
