namespace HospitalityOS.Capabilities.Retrieval;

/// <summary>
/// Tuning for <c>Property.SearchContent</c> (configuration section <see cref="SectionName"/>).
/// </summary>
public sealed class ContentSearchOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Search:Content";

    /// <summary>
    /// Least cosine similarity a semantic match needs, so unrelated content is not returned as the "nearest" answer. It
    /// depends on the embedding model: set it with the <c>content.embed</c> route. Default 0.3.
    /// </summary>
    public double MinSimilarity { get; set; } = 0.3;
}
