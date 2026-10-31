namespace HospitalityOS.Contracts.Api.Staff;

/// <summary>
/// Puts a staff member on or off duty.
/// </summary>
/// <param name="IsOnDuty">Whether they are on duty; required.</param>
public sealed record SetDutyRequest(bool? IsOnDuty = null);
