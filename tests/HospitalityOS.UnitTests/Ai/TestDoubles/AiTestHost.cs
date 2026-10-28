using System.ComponentModel;
using HospitalityOS.Ai;
using HospitalityOS.Bindings;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Capabilities.Registry;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace HospitalityOS.UnitTests.Ai.TestDoubles;

/// <summary>
/// A service provider with the AI layer over inference routing (task <see cref="Concierge"/> for chat and
/// <see cref="Extract"/> for extraction, and <see cref="Grounded"/> for grounded answering with the hours tool, all on the
/// scripted provider and always declared) and a capability registry holding
/// <see cref="Capabilities"/>; the executor is <see cref="RecordingExecutor"/>.
/// </summary>
internal static class AiTestHost
{
    public const string Concierge = "test.concierge";
    public const string Extract = "test.extract";
    public const string Grounded = "test.grounded";

    /// <summary>
    /// Hours v1 and v2 (read-only, agents allowed), a staff-only read, a write agents may call, and the two reads grounded
    /// answering builds its context from.
    /// </summary>
    public static IReadOnlyList<CapabilityDefinition> Capabilities { get; } =
    [
        HoursLookupCapability.Define("Test.GetHours", 1, "When an amenity is open."),
        HoursLookupCapability.Define("Test.GetHours", 2, "When an amenity is open, with notes."),
        HoursLookupCapability.Define("Test.GetSecret", 1, "Staff-only figures.", callers: [CapabilityCaller.Manager]),
        HoursLookupCapability.Define("Test.BookTable", 1, "Books a table.", SideEffect.PlatformState),
        HoursLookupCapability.Define("Property.GetProperty", 1, "The property's profile."),
        HoursLookupCapability.Define("Property.SearchContent", 1, "Published content matching words."),
    ];

    public static ServiceProvider Build(ScriptedModel model, ExecutorScript executor, Action<IServiceCollection> declare)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Inference:Providers:scripted:Adapter"] = "Scripted",
            [$"Inference:Tasks:{Concierge}:Provider"] = "scripted",
            [$"Inference:Tasks:{Concierge}:Model"] = "scripted-model",
            [$"Inference:Tasks:{Extract}:Provider"] = "scripted",
            [$"Inference:Tasks:{Extract}:Model"] = "scripted-model",
            [$"Inference:Tasks:{Grounded}:Provider"] = "scripted",
            [$"Inference:Tasks:{Grounded}:Model"] = "scripted-model",
        }).Build();
        ServiceCollection services = new();

        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 28, 18, 0, 0, TimeSpan.Zero)));
        services.AddScoped<TenantContext>();
        services.AddScoped(_ => new OperationContext(new FixedIdGenerator(Guid.Parse("01890a5d-ac96-774b-bcce-0000000000ff"))));
        services.AddInference(configuration);
        services.AddPrompts(typeof(AiTestHost).Assembly);
        services.AddSingleton<IAdapterFactory<IChatModel>>(new ScriptedModelFactory<IChatModel>(model));
        services.AddSingleton<IAdapterFactory<IStructuredExtractor>>(new ScriptedModelFactory<IStructuredExtractor>(model));
        services.AddSingleton(new CapabilityRegistry(Capabilities));
        services.AddSingleton(executor);
        services.AddScoped<ICapabilityExecutor, RecordingExecutor>();
        services.AddExtraction(Extract, "test.extract");
        services.AddGroundedAnswering(Grounded, "test.grounded", ["Test.GetHours@1"]);
        declare(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}

/// <summary>
/// The input of the test hours capability.
/// </summary>
internal sealed record HoursLookupInput([property: Description("The amenity, e.g. pool.")] string Amenity);

/// <summary>
/// The output of the test hours capability.
/// </summary>
internal sealed record HoursLookupOutput(string Opens, string Closes);

/// <summary>
/// A capability declaration for tool loop tests; the executor is a test double, so it never runs.
/// </summary>
internal sealed class HoursLookupCapability : ICapability<HoursLookupInput, HoursLookupOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "Test.GetHours",
        Version = 1,
        Description = "When an amenity is open.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Guest),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.None,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };

    public static CapabilityDefinition Define(string name, int version, string description, SideEffect sideEffects = SideEffect.None, CapabilityCaller[]? callers = null) =>
        CapabilityDefinition.Create(typeof(HoursLookupCapability), Descriptor with
        {
            Name = name,
            Version = version,
            Description = description,
            SideEffects = sideEffects,
            Authorization = CapabilityAuthorization.Allow(callers ?? [CapabilityCaller.Agent, CapabilityCaller.Guest]),
        });

    public Task<Result<HoursLookupOutput>> ExecuteAsync(HoursLookupInput input, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Declaration only.");
}
