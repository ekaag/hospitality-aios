using HospitalityOS.Ports.Inference;
using Xunit;

namespace HospitalityOS.UnitTests.Ports.Inference;

public sealed class PromptTests
{
    private static readonly Prompt concierge = new(
        new PromptVersion("concierge.answer", 2, "sha256:00"),
        "You are the concierge at {{property}}. Today is {{date}}.\nGuests at {{property}} expect brevity. Keep {braces} and {{ spaced }} as they are.\n");

    [Fact]
    public void Variables_AreThePlaceholdersNames_DistinctInOrderOfFirstUse()
    {
        Assert.Equal(["property", "date"], concierge.Variables);
    }

    [Fact]
    public void Render_FillsEveryPlaceholder_AndLeavesOtherBracesAlone()
    {
        string rendered = concierge.Render(new Dictionary<string, string> { ["property"] = "Aurora Grand", ["date"] = "28 October" });

        Assert.Equal("You are the concierge at Aurora Grand. Today is 28 October.\nGuests at Aurora Grand expect brevity. Keep {braces} and {{ spaced }} as they are.\n", rendered);
    }

    [Fact]
    public void Render_InsertsValuesAsTheyAre_SoAPlaceholderInAValueIsNotFilledIn()
    {
        string rendered = concierge.Render(new Dictionary<string, string> { ["property"] = "{{date}}", ["date"] = "28 October" });

        Assert.StartsWith("You are the concierge at {{date}}. Today is 28 October.", rendered, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("property", null, "Missing: date. Unknown: (none).")]
    [InlineData("property", "Date", "Missing: date. Unknown: Date.")]
    [InlineData("property", "guest", "Missing: date. Unknown: guest.")]
    public void Render_WithoutAValueForEachVariableOrWithAnUnknownOne_Throws(string first, string? second, string expected)
    {
        Dictionary<string, string> values = new() { [first] = "x" };

        if (second is not null)
        {
            values[second] = "y";
        }

        ArgumentException exception = Assert.Throws<ArgumentException>(() => concierge.Render(values));

        Assert.Contains("Prompt 'concierge.answer@2' needs values for exactly: property, date.", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WithEveryValueAndAnUnknownOne_Throws()
    {
        Dictionary<string, string> values = new() { ["property"] = "Aurora Grand", ["date"] = "28 October", ["guest"] = "Ms Rao" };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => concierge.Render(values));

        Assert.Contains("Missing: (none). Unknown: guest.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_APromptWithoutPlaceholders_NeedsNoValues()
    {
        Prompt plain = new(new PromptVersion("intent.classify", 1, "sha256:00"), "Route the guest's message.");

        Assert.Equal("Route the guest's message.", plain.Render(new Dictionary<string, string>()));
    }

    [Fact]
    public void PromptVersion_ReadsAsNameAtNumber()
    {
        Assert.Equal("concierge.answer@2", concierge.Version.ToString());
    }
}
