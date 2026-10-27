using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// Output of <c>Property.SearchContent</c>; empty means nothing published matches.
/// </summary>
/// <param name="Results">Best matches first.</param>
public sealed record SearchContentOutput(
    ValueList<ContentEntry> Results);
