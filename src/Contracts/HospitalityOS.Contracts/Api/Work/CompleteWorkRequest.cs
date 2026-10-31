namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// Completes work.
/// </summary>
/// <param name="Note">The worker's note, e.g. <c>Left at the door</c>.</param>
public sealed record CompleteWorkRequest(string? Note = null);
