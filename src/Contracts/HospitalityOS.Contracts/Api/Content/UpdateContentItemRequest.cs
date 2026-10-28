using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Content;

/// <summary>
/// <c>PUT /api/v1/content/{id}</c>: replaces a content item's text; its status does not change.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Type">What kind of text it is; required.</param>
/// <param name="Title">Title, or the question for an FAQ; required.</param>
/// <param name="Body">The text, or the answer for an FAQ; required.</param>
/// <param name="About">The subject it describes; omit for the property in general.</param>
/// <param name="Tags">Search keywords.</param>
public sealed record UpdateContentItemRequest(
    ContentType? Type = null,
    string? Title = null,
    string? Body = null,
    SubjectReference? About = null,
    ValueList<string>? Tags = null);
