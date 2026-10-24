namespace HospitalityOS.Ports.Bindings;

/// <summary>
/// Result of an adapter health probe.
/// </summary>
/// <param name="IsHealthy">Whether the adapter can serve requests.</param>
/// <param name="Description">Optional detail, e.g. the failure reason.</param>
public sealed record AdapterHealth(bool IsHealthy, string? Description = null)
{
    /// <summary>
    /// A healthy result with no detail.
    /// </summary>
    public static AdapterHealth Healthy { get; } = new(true);

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    /// <param name="description">Why the adapter is unhealthy.</param>
    /// <returns>The unhealthy result.</returns>
    public static AdapterHealth Unhealthy(string description) => new(false, description);
}
