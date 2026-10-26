namespace HospitalityOS.SharedKernel.Execution;

/// <summary>
/// A staff member's role (plan §3), which decides what they may do.
/// </summary>
public enum StaffRole
{
    /// <summary>
    /// Performs physical work: accepts, starts, and completes HumanActions.
    /// </summary>
    Worker,

    /// <summary>
    /// Supervises operations at a property.
    /// </summary>
    Manager,

    /// <summary>
    /// Configures properties.
    /// </summary>
    Admin,
}
