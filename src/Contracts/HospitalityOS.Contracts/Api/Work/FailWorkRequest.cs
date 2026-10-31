namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// Fails work.
/// </summary>
/// <param name="Reason">Why it cannot be done, e.g. <c>Could not access the room</c>; required.</param>
public sealed record FailWorkRequest(string? Reason = null);
