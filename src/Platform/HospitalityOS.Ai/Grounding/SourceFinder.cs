using System.Text.Json;
using HospitalityOS.Contracts.Capabilities;

namespace HospitalityOS.Ai.Grounding;

/// <summary>
/// Finds the sources in a read capability's output: every object shaped like a <see cref="SourceReference"/> (a string
/// <c>type</c>, a UUID <c>id</c>, and a whole-number <c>version</c>), however deeply nested.
/// </summary>
internal static class SourceFinder
{
    public static IEnumerable<SourceReference> In(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (AsSource(element) is { } source)
                {
                    yield return source;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    foreach (SourceReference nested in In(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    foreach (SourceReference nested in In(item))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static SourceReference? AsSource(JsonElement element) =>
        element.TryGetProperty("type", out JsonElement type) && type.ValueKind is JsonValueKind.String
        && element.TryGetProperty("id", out JsonElement id) && id.ValueKind is JsonValueKind.String && id.TryGetGuid(out Guid guid)
        && element.TryGetProperty("version", out JsonElement version) && version.ValueKind is JsonValueKind.Number && version.TryGetInt64(out long number)
            ? new SourceReference(type.GetString()!, guid, number)
            : null;
}
