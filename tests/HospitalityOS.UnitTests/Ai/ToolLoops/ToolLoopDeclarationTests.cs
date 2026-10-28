using HospitalityOS.Ai;
using HospitalityOS.UnitTests.Ai.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static HospitalityOS.UnitTests.Ai.TestDoubles.AiTestHost;

namespace HospitalityOS.UnitTests.Ai.ToolLoops;

public sealed class ToolLoopDeclarationTests
{
    [Fact]
    public void Startup_WithEveryToolRegisteredReadOnlyAndOpenToAgents_Succeeds()
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), ToolLoopTests.Declare(["Test.GetHours@2"]));

        Assert.Null(Record.Exception(() => services.GetRequiredService<IStartupValidator>().Validate()));
    }

    [Theory]
    [InlineData("Test.GetHours@3", "Tool loop 'test.concierge' allows Test.GetHours@3, which is not registered.")]
    [InlineData("Test.Unknown@1", "Tool loop 'test.concierge' allows Test.Unknown@1, which is not registered.")]
    [InlineData("Test.BookTable@1", "Tool loop 'test.concierge' allows Test.BookTable@1, which has PlatformState side effects; agents may call read-only capabilities only.")]
    [InlineData("Test.GetSecret@1", "Tool loop 'test.concierge' allows Test.GetSecret@1, which agents may not invoke.")]
    public void Startup_AToolThatIsUnregisteredHasSideEffectsOrIsClosedToAgents_Fails(string tool, string expected) =>
        AssertFails(ToolLoopTests.Declare([tool]), expected);

    [Fact]
    public void Startup_OneCapabilityAllowedTwice_OrALoopDeclaredTwice_Fails()
    {
        AssertFails(ToolLoopTests.Declare(["Test.GetHours@1", "Test.GetHours@2"]), "Tool loop 'test.concierge' allows Test.GetHours more than once; allow one version.");
        AssertFails(
            services => services.AddToolLoop(Concierge, "test.concierge", ["Test.GetHours@1"]).AddToolLoop("Test.Concierge", "test.concierge", ["Test.GetHours@2"]),
            "Tool loop 'Test.Concierge' is declared more than once.");
    }

    [Theory]
    [InlineData("Test.GetHours")]
    [InlineData("Test.GetHours@0")]
    [InlineData("Test.GetHours@latest")]
    [InlineData("test.getHours@1")]
    [InlineData("Test_GetHours@1")]
    public void AddToolLoop_AnEntryWithoutANameAndVersion_Throws(string tool)
    {
        ServiceCollection services = new();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => services.AddToolLoop(Concierge, null, [tool]));

        Assert.Contains("must name a capability and its version, e.g. Property.GetOperatingHours@1", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void AddToolLoop_NoTools_OrARoundLimitOutOfRange_Throws(int rounds)
    {
        ServiceCollection services = new();

        Assert.Throws<ArgumentException>(() => services.AddToolLoop(Concierge, null, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddToolLoop(Concierge, null, ["Test.GetHours@1"], rounds));
    }

    private static void AssertFails(Action<IServiceCollection> declare, string expected)
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), declare);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(expected, exception.Failures);
    }
}
