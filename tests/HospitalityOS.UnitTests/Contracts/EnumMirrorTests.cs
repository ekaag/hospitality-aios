using HospitalityOS.Contracts;
using Xunit;
using Api = HospitalityOS.Contracts.Api;
using Contract = HospitalityOS.Contracts.Capabilities;
using Model = HospitalityOS.Domain;

namespace HospitalityOS.UnitTests.Contracts;

public sealed class EnumMirrorTests
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
        { typeof(Model.Properties.Spaces.SpaceKind), typeof(Api.Spaces.SpaceKind) },
        { typeof(Model.Properties.Policies.PolicyStatus), typeof(Api.Policies.PolicyStatus) },
        { typeof(Model.Properties.ContentItems.ContentStatus), typeof(Api.Content.ContentStatus) },
        { typeof(Model.Activities.PropertyEvents.PropertyEventStatus), typeof(Api.Events.PropertyEventStatus) },
        { typeof(Model.Services.Services.ServiceCategory), typeof(Api.Services.ServiceCategory) },
        { typeof(Model.Capabilities.AITraces.AITraceOutcome), typeof(Api.Ai.AITraceOutcome) },
        { typeof(Model.Guests.Conversations.ConversationStatus), typeof(Api.Conversations.ConversationStatus) },
        { typeof(Model.Guests.GuestMessages.MessageSender), typeof(Api.Conversations.MessageSender) },
        { typeof(Model.Services.ServiceRequests.ServiceRequestStatus), typeof(Contract.GuestService.RequestStatus) },
        { typeof(Model.Operations.HumanActions.HumanActionType), typeof(Contract.HumanActions.ActionType) },
        { typeof(Model.Operations.HumanActions.HumanActionPriority), typeof(Contract.HumanActions.ActionPriority) },
        { typeof(Model.Operations.HumanActions.HumanActionStatus), typeof(Contract.HumanActions.ActionStatus) },
        { typeof(Model.Guests.Notifications.GuestNotificationStatus), typeof(Contract.Messaging.NotificationStatus) },    };

    [Theory]
    [MemberData(nameof(Mirrors))]
    public void Mirror_NamesEveryDomainMemberAndNothingElse(Type domain, Type contract)
    {
        Assert.Equal(Enum.GetNames(domain), Enum.GetNames(contract));
    }

    [Fact]
    public void To_ConvertsByName()
    {
        Assert.Equal(Contract.Dining.DietaryTag.GlutenFree, EnumMirror.To<Contract.Dining.DietaryTag>(Model.Dining.Menus.DietaryTag.GlutenFree));
        Assert.Equal(Model.Properties.Shared.SubjectKind.Restaurant, EnumMirror.To<Model.Properties.Shared.SubjectKind>(Contract.SubjectKind.Restaurant));
    }
}
