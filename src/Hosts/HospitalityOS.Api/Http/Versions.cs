using System.Globalization;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Http;

/// <summary>
/// A record's version as its HTTP entity tag (<c>"3"</c>), and <c>If-Match</c> as the version a change expects, so a
/// stale edit is refused (412) instead of overwriting someone else's.
/// </summary>
internal static class Versions
{
    public static string ETag(long version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// The version in <c>If-Match</c>, <c>null</c> when absent; a malformed or multi-valued header is a validation error.
    /// </summary>
    public static Result<long?> ExpectedVersion(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? header = request.Headers.IfMatch;

        if (string.IsNullOrEmpty(header))
        {
            return (long?)null;
        }

        string tag = header.Trim();

        return tag.Length > 2 && tag[0] == '"' && tag[^1] == '"' && long.TryParse(tag[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out long version)
            ? version
            : Error.Validation("If-Match", "request.if_match_invalid", "If-Match must be one entity tag from this API, e.g. \"3\".");
    }
}
