using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Domain.Properties.ContentItems;

/// <summary>
/// A content item's text. Validated and normalized by <see cref="ContentItem"/>.
/// </summary>
/// <param name="Type">What kind of text it is.</param>
/// <param name="Title">Title, or the question for an FAQ; required.</param>
/// <param name="Body">The text, or the answer for an FAQ; required.</param>
/// <param name="About">The subject it describes; <c>null</c> for the property in general.</param>
/// <param name="Tags">Search keywords; trimmed, lower-cased, de-duplicated, sorted.</param>
public sealed record ContentItemDetails(
    ContentType Type,
    string Title,
    string Body,
    SubjectReference? About = null,
    ValueList<string>? Tags = null);
