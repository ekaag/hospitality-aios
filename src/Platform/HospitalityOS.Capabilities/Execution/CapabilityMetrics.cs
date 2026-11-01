using System.Diagnostics;
using System.Diagnostics.Metrics;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// Capability success and failure metrics (spec §37) and a span per invocation, under <see cref="Name"/>: each
/// invocation by capability (or <c>unknown</c>, so callers cannot create series) and outcome, as the audit records it:
/// <c>succeeded</c>, <c>denied</c> (unknown, not allowed, or needing confirmation), or <c>failed</c>, with its duration.
/// Singleton.
/// </summary>
internal sealed class CapabilityMetrics
{
    public const string Name = "HospitalityOS.Capabilities";
    public const string Unknown = "unknown";
    public const string Succeeded = "succeeded";
    public const string Denied = "denied";
    public const string Failed = "failed";

    private readonly Counter<long> invocations;
    private readonly Histogram<double> duration;

    public CapabilityMetrics(IMeterFactory? meters = null)
    {
        Meter meter = meters?.Create(Name) ?? new Meter(Name);

        invocations = meter.CreateCounter<long>("hospitalityos.capabilities.invocations", "{invocation}", "Capability invocations, by capability and outcome.");
        duration = meter.CreateHistogram<double>("hospitalityos.capabilities.duration", "s", "How long a capability invocation took, from lookup to commit.");
    }

    public static ActivitySource Source { get; } = new(Name);

    public static string Outcome<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Succeeded
            : result.Error.Kind is ErrorKind.Forbidden || result.Error.Code is CapabilityErrors.ConfirmationRequiredCode or CapabilityErrors.UnknownCode ? Denied
            : Failed;
    }

    public void Invoked(string capability, string outcome, TimeSpan elapsed)
    {
        TagList tags = new() { { "capability", capability }, { "outcome", outcome } };

        invocations.Add(1, tags);
        duration.Record(elapsed.TotalSeconds, tags);
    }
}
