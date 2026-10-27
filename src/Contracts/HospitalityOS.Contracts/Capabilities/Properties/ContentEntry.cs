using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// A published content item.
/// </summary>
/// <param name="Source">The content record.</param>
/// <param name="Type">Kind.</param>
/// <param name="Title">Title.</param>
/// <param name="Body">The text.</param>
/// <param name="Tags">Tags.</param>
/// <param name="About">What it is about.</param>
public sealed record ContentEntry(
    SourceReference Source,
    ContentType Type,
    string Title,
    string Body,
    ValueList<string> Tags,
    Subject? About);
