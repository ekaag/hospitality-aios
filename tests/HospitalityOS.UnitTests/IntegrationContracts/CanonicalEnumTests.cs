using Xunit;
using Integration = HospitalityOS.IntegrationContracts;
using Model = HospitalityOS.Domain;

namespace HospitalityOS.UnitTests.IntegrationContracts;

/// <summary>
/// Connector enums carry canonical values (P3), so each must name exactly the members of the domain enum it mirrors.
/// </summary>
public sealed class CanonicalEnumTests
{
    public static TheoryData<Type, Type> Mirrors => new()
    {
        { typeof(Model.Guests.StayContexts.StayStatus), typeof(Integration.Pms.StayStatus) },
        { typeof(Model.Properties.ContentItems.ContentType), typeof(Integration.Cms.ContentType) },
        { typeof(Model.Guests.Conversations.ConversationChannel), typeof(Integration.Messaging.MessageChannel) },
    };

    [Theory]
    [MemberData(nameof(Mirrors))]
    public void IntegrationEnum_NamesEveryDomainMemberAndNothingElse(Type domain, Type integration)
    {
        Assert.Equal(Enum.GetNames(domain), Enum.GetNames(integration));
    }
}
