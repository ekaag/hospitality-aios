using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Content;

/// <summary>
/// A content item, normalized (trimmed; tags lower-cased and sorted).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Type">What kind of text it is.</param>
/// <param name="Title">Title, or the question for an FAQ.</param>
/// <param name="Body">The text, or the answer for an FAQ.</param>
/// <param name="About">The subject it describes; <c>null</c> for the property in general.</param>
/// <param name="Tags">Search keywords.</param>
/// <param name="Status">Where it is in its lifecycle.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record ContentItemResponse(
    Guid Id,
    PropertyId PropertyId,
    ContentType Type,
    string Title,
    string Body,
    SubjectReference? About,
    ValueList<string> Tags,
    ContentStatus Status,
    long Version);
