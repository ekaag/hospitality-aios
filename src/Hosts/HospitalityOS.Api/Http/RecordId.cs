using System.Globalization;

namespace HospitalityOS.Api.Http;

/// <summary>
/// A record ID from a route or query string. The empty GUID is never a record's ID (typed IDs refuse it), so like any
/// value that is not a GUID it fails to bind and the request is refused as malformed (400), instead of failing later.
/// </summary>
internal readonly record struct RecordId(Guid Value)
{
    public static bool TryParse(string? value, IFormatProvider? provider, out RecordId id)
    {
        bool parsed = Guid.TryParse(value, provider ?? CultureInfo.InvariantCulture, out Guid guid) && guid != Guid.Empty;
        id = parsed ? new RecordId(guid) : default;

        return parsed;
    }
}
