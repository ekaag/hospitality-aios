using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// Completes work.
/// </summary>
/// <param name="Note">The worker's note, e.g. <c>Left at the door</c>.</param>
/// <param name="Evidence">Keys of files uploaded for this work (<c>POST /api/v1/work/{id}/evidence</c>), at most 10.</param>
public sealed record CompleteWorkRequest(string? Note = null, ValueList<string>? Evidence = null);
