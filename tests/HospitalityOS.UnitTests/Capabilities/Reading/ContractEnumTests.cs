using HospitalityOS.Capabilities.Reading;
using Xunit;
using Contract = HospitalityOS.Contracts.Capabilities;
using Model = HospitalityOS.Domain;

namespace HospitalityOS.UnitTests.Capabilities.Reading;

public sealed class ContractEnumTests
{
    public static TheoryData<Type, Type> Mirrors => new()
    {
        { typeof(Model.Properties.Shared.SubjectKind), typeof(Contract.SubjectKind) },
        { typeof(Model.Properties.Hours.HoursSource), typeof(Contract.HoursBasis) },
        { typeof(Model.Properties.Amenities.AmenityCategory), typeof(Contract.Properties.AmenityCategory) },
        { typeof(Model.Properties.Policies.PolicyType), typeof(Contract.Properties.PolicyType) },
        { typeof(Model.Properties.ContentItems.ContentType), typeof(Contract.Properties.ContentType) },
        { typeof(Model.Dining.Menus.DietaryTag), typeof(Contract.Dining.DietaryTag) },
        { typeof(Model.Activities.PropertyEvents.PropertyEventCategory), typeof(Contract.Activities.PropertyEventCategory) },
        { typeof(Model.Activities.PropertyEvents.EventAudience), typeof(Contract.Activities.EventAudience) },
    };

    [Theory]
    [MemberData(nameof(Mirrors))]
    public void ContractEnum_NamesEveryDomainMemberAndNothingElse(Type domain, Type contract)
    {
        Assert.Equal(Enum.GetNames(domain), Enum.GetNames(contract));
    }

    [Fact]
    public void To_ConvertsByName()
    {
        Assert.Equal(Contract.Dining.DietaryTag.GlutenFree, ContractEnum.To<Contract.Dining.DietaryTag>(Model.Dining.Menus.DietaryTag.GlutenFree));
        Assert.Equal(Model.Properties.Shared.SubjectKind.Restaurant, ContractEnum.To<Model.Properties.Shared.SubjectKind>(Contract.SubjectKind.Restaurant));
    }
}
