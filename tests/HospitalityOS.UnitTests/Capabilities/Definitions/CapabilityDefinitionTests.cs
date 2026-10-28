using System.Text.Json;
using System.Text.Json.Nodes;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Capabilities.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Capabilities.Definitions;

public sealed class CapabilityDefinitionTests
{
    private static readonly CapabilityDescriptor valid = RequestItemCapability.Descriptor;

    private static readonly Dictionary<string, (CapabilityDescriptor Descriptor, string Problem)> invalidCases = new()
    {
        ["lower-case name"] = (valid with { Name = "guestService.requestItem" }, "Name"),
        ["name without action"] = (valid with { Name = "GuestService" }, "Name"),
        ["name with three segments"] = (valid with { Name = "GuestService.Request.Item" }, "Name"),
        ["name with a space"] = (valid with { Name = "Guest Service.RequestItem" }, "Name"),
        ["name with an underscore"] = (valid with { Name = "Guest_Service.RequestItem" }, "Name"),
        ["empty name"] = (valid with { Name = string.Empty }, "Name"),
        ["over-long name"] = (valid with { Name = "GuestService." + new string('A', CapabilityDefinition.MaxNameLength - 12) }, "Name"),
        ["version 0"] = (valid with { Version = 0 }, "Version"),
        ["blank description"] = (valid with { Description = " " }, "Description is required"),
        ["over-long description"] = (valid with { Description = new string('d', CapabilityDefinition.MaxDescriptionLength + 1) }, "Description must be"),
        ["no callers"] = (valid with { Authorization = CapabilityAuthorization.Allow() }, "at least one caller"),
        ["duplicate caller"] = (valid with { Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Guest, CapabilityCaller.Guest) }, "more than once"),
        ["unknown caller"] = (valid with { Authorization = CapabilityAuthorization.Allow((CapabilityCaller)99) }, "unknown caller"),
        ["undefined side effect"] = (valid with { SideEffects = (SideEffect)99 }, "defined values"),
        ["undefined audit classification"] = (valid with { AuditClassification = (AuditClassification)99 }, "defined values"),
        ["read-only requiring confirmation"] = (valid with { SideEffects = SideEffect.None, Idempotency = IdempotencyPolicy.Inherent, ConfirmationPolicy = ConfirmationPolicy.Required }, "cannot require confirmation"),
        ["read-only requiring a key"] = (valid with { SideEffects = SideEffect.None, Idempotency = IdempotencyPolicy.KeyRequired }, "cannot require an idempotency key"),
        ["external without a key"] = (valid with { SideEffects = SideEffect.External, Idempotency = IdempotencyPolicy.Inherent }, "must require an idempotency key"),
    };

    public static TheoryData<string> InvalidCaseNames => new(invalidCases.Keys);

    [Fact]
    public void For_ValidCapability_CopiesTheDescriptorAndTypes()
    {
        CapabilityDefinition definition = CapabilityDefinition.For<RequestItemCapability>();

        Assert.Equal(
            ("GuestService.RequestItem", 1, valid.Description, valid.Authorization, ConfirmationPolicy.NotRequired, SideEffect.PlatformState, IdempotencyPolicy.KeyRequired, AuditClassification.Routine),
            (definition.Name, definition.Version, definition.Description, definition.Authorization, definition.ConfirmationPolicy, definition.SideEffects, definition.Idempotency, definition.AuditClassification));
        Assert.Equal((typeof(RequestItemCapability), typeof(RequestItemInput), typeof(RequestItemOutput)), (definition.CapabilityType, definition.InputType, definition.OutputType));
        Assert.Equal("GuestService.RequestItem v1", definition.ToString());
    }

    [Fact]
    public void InputSchema_DescribesTheWireFormat()
    {
        const string expected = """
            {
              "description": "A guest's request for an item.",
              "type": "object",
              "properties": {
                "propertyId": { "description": "The property the guest is staying at.", "type": "string", "format": "uuid" },
                "itemCode": { "type": "string" },
                "quantity": { "type": "integer" },
                "note": { "type": ["string", "null"] },
                "deliverTo": { "type": ["string", "null"], "format": "uuid" },
                "tags": { "type": "array", "items": { "type": "string" } },
                "urgency": { "enum": ["Whenever", "Soon", "Now"] },
                "wantedBy": { "type": ["string", "null"], "format": "date-time" },
                "budget": {
                  "type": ["object", "null"],
                  "properties": { "amount": { "type": "number" }, "currencyCode": { "type": "string" } },
                  "required": ["amount", "currencyCode"],
                  "additionalProperties": false
                }
              },
              "required": ["propertyId", "itemCode", "quantity", "note", "deliverTo", "tags", "urgency", "wantedBy", "budget"],
              "additionalProperties": false
            }
            """;

        CapabilityDefinition definition = CapabilityDefinition.For<RequestItemCapability>();

        AssertJsonEqual(expected, definition.InputSchema);
    }

    [Fact]
    public void OutputSchema_DescribesIdListsAsUuidArrays()
    {
        const string expected = """
            {
              "type": "object",
              "properties": {
                "requestId": { "type": "string", "format": "uuid" },
                "route": { "type": "array", "items": { "type": "string", "format": "uuid" } }
              },
              "required": ["requestId", "route"],
              "additionalProperties": false
            }
            """;

        CapabilityDefinition definition = CapabilityDefinition.For<RequestItemCapability>();

        AssertJsonEqual(expected, definition.OutputSchema);
    }

    [Fact]
    public void InputSchema_NamesExactlyTheMembersTheWireFormatWrites()
    {
        RequestItemInput input = new(
            new PropertyId(Guid.CreateVersion7()), "towel", 2, null, null, ValueList.From(["extra"]), RequestUrgency.Soon, null, new Money(5m, "USD"));
        CapabilityDefinition definition = CapabilityDefinition.For<RequestItemCapability>();

        JsonElement written = JsonSerializer.SerializeToElement(input, ContractJson.Strict);

        Assert.Equal(
            definition.InputSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name),
            written.EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData("""{ "requestId": "01890a5d-ac96-774b-bcce-000000000001", "route": [], "eta": "soon" }""")]
    [InlineData("""{ "requestId": "01890a5d-ac96-774b-bcce-000000000001" }""")]
    [InlineData("""{ "requestId": null, "route": [] }""")]
    public void WireFormat_RejectsWhatTheSchemaForbids(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RequestItemOutput>(json, ContractJson.Strict));
    }

    [Fact]
    public void WireFormat_RejectsANumberSentAsAString()
    {
        const string json = """
            { "propertyId": "01890a5d-ac96-774b-bcce-000000000001", "itemCode": "towel", "quantity": "2", "note": null, "deliverTo": null,
              "tags": [], "urgency": "Now", "wantedBy": null, "budget": null }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RequestItemInput>(json, ContractJson.Strict));
    }

    [Theory]
    [MemberData(nameof(InvalidCaseNames))]
    public void Create_InvalidDescriptor_FailsNamingTheProblem(string caseName)
    {
        (CapabilityDescriptor descriptor, string problem) = invalidCases[caseName];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CapabilityDefinition.Create(typeof(RequestItemCapability), descriptor));

        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_SeveralProblems_ListsEveryOne()
    {
        CapabilityDescriptor descriptor = valid with { Name = "bad", Version = 0, Description = string.Empty };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CapabilityDefinition.Create(typeof(RequestItemCapability), descriptor));

        Assert.All(["Name 'bad'", "Version 0", "Description is required"], problem => Assert.Contains(problem, error.Message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SideEffect.None, IdempotencyPolicy.Inherent, ConfirmationPolicy.NotRequired)]
    [InlineData(SideEffect.PlatformState, IdempotencyPolicy.Inherent, ConfirmationPolicy.NotRequired)]
    [InlineData(SideEffect.PlatformState, IdempotencyPolicy.KeyRequired, ConfirmationPolicy.Required)]
    [InlineData(SideEffect.External, IdempotencyPolicy.KeyRequired, ConfirmationPolicy.Required)]
    [InlineData(SideEffect.External, IdempotencyPolicy.KeyRequired, ConfirmationPolicy.NotRequired)]
    public void Create_ConsistentPolicies_Succeeds(SideEffect sideEffects, IdempotencyPolicy idempotency, ConfirmationPolicy confirmation)
    {
        CapabilityDescriptor descriptor = valid with { SideEffects = sideEffects, Idempotency = idempotency, ConfirmationPolicy = confirmation };

        CapabilityDefinition definition = CapabilityDefinition.Create(typeof(RequestItemCapability), descriptor);

        Assert.Equal((sideEffects, idempotency, confirmation), (definition.SideEffects, definition.Idempotency, definition.ConfirmationPolicy));
    }

    [Fact]
    public void Create_NameAtMaximumLength_Succeeds()
    {
        string name = "GuestService." + new string('A', CapabilityDefinition.MaxNameLength - 13);

        CapabilityDefinition definition = CapabilityDefinition.Create(typeof(RequestItemCapability), valid with { Name = name });

        Assert.Equal(CapabilityDefinition.MaxNameLength, definition.Name.Length);
    }

    [Theory]
    [InlineData(typeof(TextInputCapability), "The input String")]
    [InlineData(typeof(ListOutputCapability), "The output ValueList`1")]
    public void Create_InputOrOutputNotAnObject_Fails(Type capabilityType, string problem)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CapabilityDefinition.Create(capabilityType, valid));

        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_TypeWithoutInputAndOutput_Fails()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CapabilityDefinition.Create(typeof(UntypedCapability), valid));

        Assert.Contains("ICapability<TInput, TOutput>", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_PublishesEverySpecField()
    {
        CapabilityDefinition definition = CapabilityDefinition.For<RequestItemCapability>();

        CapabilityDescription description = definition.Describe();

        Assert.Equal(
            new CapabilityDescription(
                "GuestService.RequestItem",
                1,
                valid.Description,
                definition.InputSchema,
                definition.OutputSchema,
                CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Guest),
                ConfirmationPolicy.NotRequired,
                SideEffect.PlatformState,
                IdempotencyPolicy.KeyRequired,
                AuditClassification.Routine),
            description);
    }

    private static void AssertJsonEqual(string expected, JsonElement actual) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual.GetRawText())),
            $"Expected:{Environment.NewLine}{expected}{Environment.NewLine}Actual:{Environment.NewLine}{actual}");

    private sealed class TextInputCapability : ICapability<string, RequestItemOutput>
    {
        public static CapabilityDescriptor Descriptor => valid;

        public Task<Result<RequestItemOutput>> ExecuteAsync(string input, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Declaration only.");
    }

    private sealed class ListOutputCapability : ICapability<RequestItemInput, ValueList<string>>
    {
        public static CapabilityDescriptor Descriptor => valid;

        public Task<Result<ValueList<string>>> ExecuteAsync(RequestItemInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Declaration only.");
    }

    private sealed class UntypedCapability : ICapability
    {
        public static CapabilityDescriptor Descriptor => valid;
    }
}
