namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// A file uploaded as evidence for work, to complete it with.
/// </summary>
/// <param name="Key">Its evidence key, <c>{work}/{file}</c>, to send in <see cref="CompleteWorkRequest.Evidence"/>.</param>
/// <param name="ContentType">Its media type, e.g. <c>image/jpeg</c>.</param>
/// <param name="Length">Its size in bytes.</param>
public sealed record EvidenceResponse(string Key, string ContentType, long Length);
