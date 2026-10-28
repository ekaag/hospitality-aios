using System.Text.Json;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Capabilities;
using Xunit;

namespace HospitalityOS.UnitTests.Ai.Tracing;

public sealed class AITraceRedactorTests
{
    [Theory]
    [InlineData("Email me at alex.smith@example.co.uk please.", "Email me at [email] please.")]
    [InlineData("Call +44 20 7946 0958 tonight.", "Call [phone] tonight.")]
    [InlineData("My number is (555) 123-4567.", "My number is [phone].")]
    [InlineData("Card 4111 1111 1111 1111, expires 12/28.", "Card [card], expires 12/28.")]
    [InlineData("Charge 5555555555554444 or call 555.123.4567", "Charge [card] or call [phone]")]
    public void Redact_MasksPersonalDataInText(string text, string expected)
    {
        (AITraceBody body, bool redacted) = AITraceRedactor.Redact(Body(text));

        Assert.Equal((expected, true), (body.TriggerText, redacted));
    }

    [Theory]
    [InlineData("The pool closes at 22:00 on 2026-10-28.")]
    [InlineData("Opens 2026-10-28 09:00, closes 2026-10-28T22:00:00Z.")]
    [InlineData("Two towels to room 412, please.")]
    [InlineData("Spa day: $1,250.00 for 2 guests.")]
    [InlineData("Reference 4111 1111 1111 1112 is not a valid card.")]
    [InlineData("Record 01890a5d-ac96-774b-bcce-000000000025 and 01890000-0000-7000-8000-000000000025.")]
    public void Redact_LeavesTimesPricesIdsAndOtherNumbersAlone(string text)
    {
        AITraceBody original = Body(text);

        (AITraceBody body, bool redacted) = AITraceRedactor.Redact(original);

        Assert.Equal((text, false), (body.TriggerText, redacted));
        Assert.Equal(original, body);
    }

    [Fact]
    public void Redact_WithholdsSensitiveCapabilityData_AndMasksStringsInRoutineCapabilityData()
    {
        JsonElement routineOutput = Json("""{"name":"Alex","email":"alex@example.com","phones":["+44 20 7946 0958"],"room":412,"closes":"22:00"}""");
        AITraceBody original = Body("Hi") with
        {
            CapabilityCalls =
            [
                Call("Guest.GetProfile@1", AuditClassification.Sensitive, Json("""{"guestId":"01890a5d-ac96-774b-bcce-000000000025"}"""), Json("""{"name":"Alex"}""")),
                Call("Property.GetOperatingHours@1", AuditClassification.Routine, Json("""{"subject":"Amenity"}"""), routineOutput),
            ],
        };

        (AITraceBody body, bool redacted) = AITraceRedactor.Redact(original);

        Assert.True(redacted);
        Assert.Equal(((JsonElement?)null, (JsonElement?)null, true), (body.CapabilityCalls[0].Input, body.CapabilityCalls[0].Output, body.CapabilityCalls[0].Withheld));
        Assert.Equal("""{"subject":"Amenity"}""", body.CapabilityCalls[1].Input?.GetRawText());
        Assert.Equal(
            """{"name":"Alex","email":"[email]","phones":["[phone]"],"room":412,"closes":"22:00"}""",
            body.CapabilityCalls[1].Output?.GetRawText());
        Assert.False(body.CapabilityCalls[1].Withheld);
    }

    [Fact]
    public void Redact_MaskingOnlyAJsonValue_StillMarksTheTraceRedacted()
    {
        AITraceBody original = Body("Hi") with
        {
            CapabilityCalls = [Call("Property.SearchContent@1", AuditClassification.Routine, Json("""{"query":"alex@example.com"}"""), Json("""{"results":[]}"""))],
        };

        (AITraceBody body, bool redacted) = AITraceRedactor.Redact(original);

        Assert.Equal(("""{"query":"[email]"}""", true), (body.CapabilityCalls[0].Input?.GetRawText(), redacted));
    }

    [Fact]
    public void Redact_MasksEveryTextTheModelSawOrWrote()
    {
        TraceMessage asked = new(TraceMessageRole.User, [new TracePart(TracePartType.Text, Text: "I'm alex@example.com")]);
        TraceMessage called = new(TraceMessageRole.Assistant, [new TracePart(TracePartType.ToolCall, CallId: "call-1", Name: "Guest_Find", Arguments: Json("""{"email":"alex@example.com"}"""))]);
        TraceMessage result = new(TraceMessageRole.Tool, [new TracePart(TracePartType.ToolResult, Text: """{"phone":"+44 20 7946 0958"}""", CallId: "call-1", IsError: false)]);
        ModelCall call = new(
            ModelCallKind.Chat, "concierge.answer", [], "Guest alex@example.com is in 412.", true, [asked, result], ["Guest_Find"], true, called, TraceStopReason.ToolCalls, null, null);
        AITraceBody original = Body("Hi") with
        {
            ModelCalls = [call],
            Grounding = new GroundingCheck([new ReplyVerdict(false, "It quotes alex@example.com.")]),
            Response = new TraceResponse("We emailed alex@example.com.", []),
        };

        (AITraceBody body, bool redacted) = AITraceRedactor.Redact(original);

        ModelCall shown = Assert.Single(body.ModelCalls);
        Assert.True(redacted);
        Assert.Equal("Guest [email] is in 412.", shown.SystemPrompt);
        Assert.Equal("I'm [email]", shown.Input[0].Parts[0].Text);
        Assert.Equal("""{"phone":"[phone]"}""", shown.Input[1].Parts[0].Text);
        Assert.Equal("""{"email":"[email]"}""", shown.Output!.Parts[0].Arguments?.GetRawText());
        Assert.Equal(("call-1", "Guest_Find"), (shown.Output.Parts[0].CallId, shown.Output.Parts[0].Name));
        Assert.Equal("It quotes [email].", body.Grounding!.Replies[0].Rejection);
        Assert.Equal("We emailed [email].", body.Response.Text);
    }

    private static AITraceBody Body(string triggerText) =>
        new(triggerText, [], [], [], null, new TraceResponse(null, []), new TokenUsage(0, 0));

    private static CapabilityCall Call(string capability, AuditClassification classification, JsonElement input, JsonElement output) =>
        new(CapabilityCallPurpose.Tool, capability, "call-1", classification, AuthorizationDecision.Allowed, input, output, Guid.Parse("01890a5d-ac96-774b-bcce-000000000099"), null);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
