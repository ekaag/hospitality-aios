using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// How a <see cref="HumanAction"/> was completed (spec §7 completionData/evidence; Journey 14).
/// </summary>
/// <param name="CompletedBy">The worker who completed it.</param>
/// <param name="CompletedAt">When (UTC).</param>
/// <param name="Note">The worker's note, e.g. "Left at the door"; optional.</param>
/// <param name="Evidence">Blob keys of photos or files in <c>IBlobStore</c>; optional.</param>
public sealed record HumanActionCompletion(StaffMemberId CompletedBy, DateTimeOffset CompletedAt, string? Note, ValueList<string> Evidence);
