namespace HospitalityOS.Workflows.Routing;

/// <summary>
/// How the HumanAction Router reroutes work (section <c>Workflows:Routing</c>).
/// </summary>
public sealed class HumanActionRoutingOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Workflows:Routing";

    /// <summary>
    /// How long an assignee has to accept work before it goes to someone else eligible; 10 minutes by default.
    /// </summary>
    public TimeSpan AcceptanceTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
