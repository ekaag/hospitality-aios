using HospitalityOS.Ports.Bindings;

namespace HospitalityOS.UnitTests.Bindings.TestDoubles;

/// <summary>
/// Sample adapter: greets with a fixed salutation and punctuation, and reports configurable health.
/// </summary>
internal sealed class Greeter : IGreeter, IAdapterHealthProbe
{
    private readonly string salutation;
    private readonly string punctuation;
    private readonly bool isHealthy;

    public Greeter(string salutation, string punctuation, bool isHealthy)
    {
        this.salutation = salutation;
        this.punctuation = punctuation;
        this.isHealthy = isHealthy;
    }

    public string Greet(string name) => $"{salutation} {name}{punctuation}";

    public Task<AdapterHealth> CheckHealthAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(isHealthy ? AdapterHealth.Healthy : AdapterHealth.Unhealthy($"{salutation} greeter is down"));
    }
}
