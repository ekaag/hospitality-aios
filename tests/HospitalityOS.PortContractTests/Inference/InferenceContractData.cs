using System.Text.Json;
using HospitalityOS.Ports.Inference;

namespace HospitalityOS.PortContractTests.Inference;

/// <summary>
/// Inputs the inference contract suites share. Written so a real model passes them as readily as a deterministic one:
/// each asks for something unambiguous.
/// </summary>
internal static class InferenceContractData
{
    public static ChatTool LocalTimeTool { get; } = new(
        "get_local_time",
        "Returns the hotel's current local time. Always call it when asked what time it is; never guess the time.",
        JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone());

    public const string ClassifierInstructions =
        "Classify the guest's message with one label.\n"
        + "service_request: the guest asks staff to bring, deliver, or fix something.\n"
        + "question: the guest asks for information, such as what time something opens or will close, or where it is.";

    public static JsonElement TowelRequestSchema { get; } = JsonDocument.Parse(
        """
        {"type":"object",
         "properties":{"item":{"type":"string","description":"What the guest wants"},"quantity":{"type":"integer","description":"How many"}},
         "required":["item","quantity"]}
        """).RootElement.Clone();
}
