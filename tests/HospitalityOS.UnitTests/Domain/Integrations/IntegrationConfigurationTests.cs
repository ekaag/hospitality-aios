using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Integrations;

public sealed class IntegrationConfigurationTests
{
    private static readonly IntegrationConfigurationId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209906001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209906002"));
    private static readonly PropertyId auroraGrand = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209906003"));
    private static readonly DateTimeOffset now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_OrdersSettingsByNameAndRaisesCreated()
    {
        IntegrationConfiguration configuration = IntegrationConfiguration.Create(
            id, tenantId, auroraGrand, ConnectorKind.Pms, "MockPms", [new("propertyCode", "AG"), new("apiKeySecret", "pms-key")], now).Value;

        Assert.Equal((ConnectorKind.Pms, "MockPms", IntegrationStatus.Active, true), (configuration.Kind, configuration.ProviderName, configuration.Status, configuration.IsActive));
        Assert.Equal(["apiKeySecret", "propertyCode"], configuration.Settings.Select(setting => setting.Name));
        Assert.Equal([new IntegrationConfigurationCreated(id, auroraGrand, ConnectorKind.Pms, "MockPms", IntegrationStatus.Active, now)], configuration.DomainEvents);
    }

    [Fact]
    public void Create_WithInvalidKindProviderAndSettings_ReportsAll()
    {
        Result<IntegrationConfiguration> result = IntegrationConfiguration.Create(
            id,
            tenantId,
            auroraGrand,
            (ConnectorKind)99,
            "Mock Pms",
            [new("hotel code", "AG"), new("region", null!), new("Region", "eu"), new("notes", new string('x', IntegrationConfiguration.MaxSettingValueLength + 1))],
            now);

        Assert.Equal(
            [
                ("kind", "integration.kind_invalid"),
                ("providerName", "integration.provider_invalid"),
                ("settings[0].name", "integration.setting_name_invalid"),
                ("settings[1].value", "integration.setting_value_required"),
                ("settings[2].name", "integration.setting_duplicate"),
                ("settings[3].value", "integration.setting_value_too_long"),
            ],
            FieldCodes(result));
    }

    [Theory]
    [InlineData("", "integration.provider_required")]
    [InlineData("9Pms", "integration.provider_invalid")]
    [InlineData("Mock-Pms", "integration.provider_invalid")]
    public void Create_WithBadProviderName_IsInvalid(string providerName, string code)
    {
        Result<IntegrationConfiguration> result = IntegrationConfiguration.Create(id, tenantId, auroraGrand, ConnectorKind.Pms, providerName, [], now);

        Assert.Equal([("providerName", code)], FieldCodes(result));
    }

    [Fact]
    public void Create_WithTooManySettings_IsInvalid()
    {
        IEnumerable<IntegrationSetting> settings = Enumerable.Range(0, IntegrationConfiguration.MaxSettings + 1).Select(index => new IntegrationSetting($"s{index}", "v"));

        Result<IntegrationConfiguration> result = IntegrationConfiguration.Create(id, tenantId, auroraGrand, ConnectorKind.Pms, "MockPms", settings, now);

        Assert.Equal([("settings", "integration.settings_too_many")], FieldCodes(result));
    }

    [Fact]
    public void Reconfigure_ToAnotherProvider_RaisesUpdated()
    {
        IntegrationConfiguration configuration = CreatePms();

        Result result = configuration.Reconfigure("OperaCloud", [new("hotelId", "8812")], now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(("OperaCloud", 2L), (configuration.ProviderName, configuration.Version));
        Assert.Equal(new IntegrationConfigurationUpdated(id, ConnectorKind.Pms, "OperaCloud", IntegrationStatus.Active, now.AddHours(1)), configuration.DomainEvents[^1]);
    }

    [Fact]
    public void Reconfigure_WithSameProviderAndSettingsInAnotherOrder_IsNoOp()
    {
        IntegrationConfiguration configuration = IntegrationConfiguration.Create(
            id, tenantId, auroraGrand, ConnectorKind.Pms, "MockPms", [new("a", "1"), new("b", "2")], now).Value;

        Result result = configuration.Reconfigure("MockPms", [new("b", "2"), new("a", "1")], now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, configuration.Version);
    }

    [Fact]
    public void Reconfigure_WithInvalidSettings_ChangesNothing()
    {
        IntegrationConfiguration configuration = CreatePms();

        Result result = configuration.Reconfigure("OperaCloud", [new("a", "1"), new("A", "2")], now.AddHours(1));

        Assert.Equal([("settings[1].name", "integration.setting_duplicate")], FieldCodes(result));
        Assert.Equal(("MockPms", 1L), (configuration.ProviderName, configuration.Version));
    }

    [Fact]
    public void DisableAndEnable_RaiseUpdatedOnceEach()
    {
        IntegrationConfiguration configuration = CreatePms();

        configuration.Disable(now.AddHours(1));
        configuration.Disable(now.AddHours(2));
        bool activeWhileDisabled = configuration.IsActive;
        configuration.Enable(now.AddHours(3));
        configuration.Enable(now.AddHours(4));

        Assert.False(activeWhileDisabled);
        Assert.Equal(
            [IntegrationStatus.Disabled, IntegrationStatus.Active],
            configuration.DomainEvents.OfType<IntegrationConfigurationUpdated>().Select(updated => updated.Status));
    }

    private static IntegrationConfiguration CreatePms() =>
        IntegrationConfiguration.Create(id, tenantId, auroraGrand, ConnectorKind.Pms, "MockPms", [new("hotelCode", "AG01")], now).Value;

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
