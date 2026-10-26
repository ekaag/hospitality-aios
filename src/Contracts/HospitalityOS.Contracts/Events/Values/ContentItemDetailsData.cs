using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A content item's information.
/// </summary>
/// <param name="Type">FAQ, overview, ... (domain enum name).</param>
/// <param name="Title">Title.</param>
/// <param name="Body">Body text.</param>
/// <param name="About">What it is about, if anything.</param>
/// <param name="Tags">Normalized tags.</param>
public sealed record ContentItemDetailsData(
    string Type,
    string Title,
    string Body,
    SubjectReferenceData? About,
    ValueList<string> Tags);
