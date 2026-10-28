using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// Redacts a trace for a reader who may not see it whole (see <see cref="AITraceRedactionOptions"/>): withholds the input
/// and output of sensitive capabilities, and masks email addresses, phone numbers, and payment card numbers in every text
/// and JSON string value. Pattern masking is best effort: it finds what looks like personal data, not every name or detail
/// a guest may write.
/// </summary>
internal sealed partial class AITraceRedactor
{
    public const string EmailMask = "[email]";
    public const string PhoneMask = "[phone]";
    public const string CardMask = "[card]";

    private const int MinPhoneDigits = 9;
    private const int MaxPhoneDigits = 15;

    private bool redacted;

    private AITraceRedactor()
    {
    }

    /// <summary>
    /// The trace as a restricted reader may see it, and whether anything was withheld or masked.
    /// </summary>
    public static (AITraceBody Body, bool Redacted) Redact(AITraceBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        AITraceRedactor redactor = new();
        AITraceBody result = redactor.Body(body);

        return (result, redactor.redacted);
    }

    private static bool IsCardNumber(string candidate)
    {
        // Luhn: double every second digit from the right; valid numbers sum to a multiple of 10.
        int[] digits = [.. candidate.Where(char.IsAsciiDigit).Select(digit => digit - '0')];
        int sum = 0;

        for (int index = 0; index < digits.Length; index++)
        {
            int digit = digits[digits.Length - 1 - index];

            if (index % 2 == 1)
            {
                digit *= 2;
                digit = digit > 9 ? digit - 9 : digit;
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }

    private static bool IsPhoneNumber(string candidate)
    {
        int digits = candidate.Count(char.IsAsciiDigit);

        // Dates such as 2026-10-28 (followed by a time) look like numbers with separators; they are not phone numbers.
        return digits is >= MinPhoneDigits and <= MaxPhoneDigits && !DatePattern().IsMatch(candidate);
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<![\w-])\d(?:[ -]?\d){12,18}(?![\w-])", RegexOptions.CultureInvariant)]
    private static partial Regex CardPattern();

    [GeneratedRegex(@"(?<![\w-])\+?\(?\d[\d ().-]{6,}\d(?![\w-])", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    private AITraceBody Body(AITraceBody body) => body with
    {
        TriggerText = OptionalText(body.TriggerText),
        CapabilityCalls = ValueList.From(body.CapabilityCalls.Select(CapabilityCall)),
        ModelCalls = ValueList.From(body.ModelCalls.Select(ModelCall)),
        Grounding = body.Grounding is { } grounding
            ? new GroundingCheck(ValueList.From(grounding.Replies.Select(reply => reply with { Rejection = OptionalText(reply.Rejection) })))
            : null,
        Response = body.Response with { Text = OptionalText(body.Response.Text) },
    };

    private CapabilityCall CapabilityCall(CapabilityCall call)
    {
        if (call.Classification is AuditClassification.Sensitive && (call.Input is not null || call.Output is not null))
        {
            redacted = true;

            return call with { Input = null, Output = null, Withheld = true };
        }

        return call with { Input = Json(call.Input), Output = Json(call.Output) };
    }

    private ModelCall ModelCall(ModelCall call) => call with
    {
        SystemPrompt = OptionalText(call.SystemPrompt),
        Input = ValueList.From(call.Input.Select(Message)),
        Output = call.Output is { } output ? Message(output) : null,
    };

    private TraceMessage Message(TraceMessage message) =>
        message with { Parts = ValueList.From(message.Parts.Select(part => part with { Text = OptionalText(part.Text), Arguments = Json(part.Arguments) })) };

    private string? OptionalText(string? text) => text is null ? null : Text(text);

    private string Text(string text)
    {
        string masked = EmailPattern().Replace(text, EmailMask);
        masked = CardPattern().Replace(masked, match => IsCardNumber(match.Value) ? CardMask : match.Value);
        masked = PhonePattern().Replace(masked, match => IsPhoneNumber(match.Value) ? PhoneMask : match.Value);

        if (!string.Equals(masked, text, StringComparison.Ordinal))
        {
            redacted = true;
        }

        return masked;
    }

    private JsonElement? Json(JsonElement? element)
    {
        if (element is not { } value)
        {
            return null;
        }

        bool before = redacted;
        redacted = false;
        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            Write(writer, value);
        }

        bool changed = redacted;
        redacted = before || changed;

        if (!changed)
        {
            return value;
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);

        return document.RootElement.Clone();
    }

    private void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (JsonElement item in element.EnumerateArray())
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(Text(element.GetString()!));
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
