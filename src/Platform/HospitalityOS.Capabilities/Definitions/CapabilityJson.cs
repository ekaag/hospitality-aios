using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalityOS.Contracts.Serialization;

namespace HospitalityOS.Capabilities.Definitions;

/// <summary>
/// The wire format for capability inputs and outputs: <see cref="ContractJson"/>, plus rejecting members a type does
/// not declare, so an agent's misspelled or invented argument is refused rather than silently ignored. The published
/// schemas follow from these options (<c>additionalProperties: false</c>).
/// </summary>
internal static class CapabilityJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(ContractJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
