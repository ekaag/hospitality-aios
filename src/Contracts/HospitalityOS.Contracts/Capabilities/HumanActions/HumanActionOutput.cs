namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Output of the human action capabilities: the action after the call.
/// </summary>
/// <param name="Action">The action.</param>
public sealed record HumanActionOutput(HumanActionEntry Action);
