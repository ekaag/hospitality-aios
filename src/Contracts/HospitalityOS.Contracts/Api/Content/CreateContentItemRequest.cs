using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Content;

/// <summary>
/// <c>POST /api/v1/content</c>: drafts descriptive text (overview, FAQ, directions, announcement) that the concierge
/// searches once published. Rules belong in policies, not content.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="Type">What kind of text it is; required.</param>
/// <param name="Title">Title, or the question for an FAQ; required.</param>
/// <param name="Body">The text, or the answer for an FAQ; required.</param>
/// <param name="About">The subject it describes; omit for the property in general.</param>
/// <param name="Tags">Search keywords; stored trimmed, lower-cased, de-duplicated, and sorted.</param>
public sealed record CreateContentItemRequest(
    PropertyId? PropertyId = null,
    ContentType? Type = null,
    string? Title = null,
    string? Body = null,
    SubjectReference? About = null,
    ValueList<string>? Tags = null);
