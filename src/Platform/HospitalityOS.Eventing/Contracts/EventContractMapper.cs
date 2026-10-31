using HospitalityOS.Contracts.Events.Activities;
using HospitalityOS.Contracts.Events.Capabilities;
using HospitalityOS.Contracts.Events.Dining;
using HospitalityOS.Contracts.Events.Guests;
using HospitalityOS.Contracts.Events.Integrations;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Contracts.Events.Organizations;
using HospitalityOS.Contracts.Events.Properties;
using HospitalityOS.Contracts.Events.Services;
using HospitalityOS.Domain.Activities.PropertyEvents.Events;
using HospitalityOS.Domain.Capabilities.AITraces.Events;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations.Events;
using HospitalityOS.Domain.Dining.Menus.Events;
using HospitalityOS.Domain.Dining.Restaurants.Events;
using HospitalityOS.Domain.Guests.Conversations.Events;
using HospitalityOS.Domain.Guests.GuestMessages.Events;
using HospitalityOS.Domain.Guests.Guests.Events;
using HospitalityOS.Domain.Guests.Notifications.Events;
using HospitalityOS.Domain.Guests.StayContexts.Events;
using HospitalityOS.Domain.Integrations.ExternalReferences.Events;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations.Events;
using HospitalityOS.Domain.Operations.HumanActions.Events;
using HospitalityOS.Domain.Operations.StaffMembers.Events;
using HospitalityOS.Domain.Organizations.Organizations.Events;
using HospitalityOS.Domain.Properties.AccommodationTypes.Events;
using HospitalityOS.Domain.Properties.AccommodationUnits.Events;
using HospitalityOS.Domain.Properties.Amenities.Events;
using HospitalityOS.Domain.Properties.ContentItems.Events;
using HospitalityOS.Domain.Properties.Hours.Events;
using HospitalityOS.Domain.Properties.Policies.Events;
using HospitalityOS.Domain.Properties.Properties.Events;
using HospitalityOS.Domain.Properties.Spaces.Events;
using HospitalityOS.Domain.Services.ServiceRequests.Events;
using HospitalityOS.Domain.Services.Services.Events;
using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Eventing.Contracts;

/// <summary>
/// Translates each domain event into its published contract (P1, P9). Generated from one table, with
/// <see cref="ContractValues"/>, so every event maps the same way; a domain event without a case here fails
/// the completeness test.
/// </summary>
internal static class EventContractMapper
{
    public static MappedEvent Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return domainEvent switch
        {
            OrganizationCreated e => new(new OrganizationCreatedV1(e.OrganizationId, e.Name), e.OrganizationId),
            OrganizationUpdated e => new(new OrganizationUpdatedV1(e.OrganizationId, e.Name), e.OrganizationId),
            PropertyCreated e => new(new PropertyCreatedV1(e.PropertyId, e.OrganizationId, ContractValues.Data(e.Details)), e.OrganizationId),
            PropertyUpdated e => new(new PropertyUpdatedV1(e.PropertyId, ContractValues.Data(e.Details))),
            SpaceCreated e => new(new SpaceCreatedV1(e.SpaceId, e.PropertyId, e.ParentSpaceId, ContractValues.Data(e.Details))),
            SpaceUpdated e => new(new SpaceUpdatedV1(e.SpaceId, ContractValues.Data(e.Details))),
            AccommodationTypeCreated e => new(new AccommodationTypeCreatedV1(e.AccommodationTypeId.Value, e.PropertyId, ContractValues.Data(e.Details))),
            AccommodationTypeUpdated e => new(new AccommodationTypeUpdatedV1(e.AccommodationTypeId.Value, ContractValues.Data(e.Details))),
            AccommodationUnitCreated e => new(new AccommodationUnitCreatedV1(e.AccommodationUnitId, e.PropertyId, e.AccommodationTypeId.Value, e.SpaceId, e.UnitNumber)),
            AccommodationUnitUpdated e => new(new AccommodationUnitUpdatedV1(e.AccommodationUnitId, e.AccommodationTypeId.Value, e.UnitNumber)),
            AmenityCreated e => new(new AmenityCreatedV1(e.AmenityId.Value, e.PropertyId, ContractValues.Data(e.Details), e.LocationSpaceId)),
            AmenityUpdated e => new(new AmenityUpdatedV1(e.AmenityId.Value, ContractValues.Data(e.Details), e.LocationSpaceId)),
            ContentItemCreated e => new(new ContentItemCreatedV1(e.ContentItemId.Value, e.PropertyId, ContractValues.Data(e.Details), e.Status.ToString())),
            ContentItemUpdated e => new(new ContentItemUpdatedV1(e.ContentItemId.Value, ContractValues.Data(e.Details), e.Status.ToString())),
            OperatingHoursCreated e => new(new OperatingHoursCreatedV1(e.OperatingHoursId.Value, e.PropertyId, ContractValues.Data(e.Subject), ContractValues.Data(e.Schedule))),
            OperatingHoursUpdated e => new(new OperatingHoursUpdatedV1(e.OperatingHoursId.Value, ContractValues.Data(e.Schedule))),
            PolicyCreated e => new(new PolicyCreatedV1(e.PolicyId.Value, e.PropertyId, ContractValues.Data(e.Details), e.Status.ToString())),
            PolicyUpdated e => new(new PolicyUpdatedV1(e.PolicyId.Value, ContractValues.Data(e.Details), e.Status.ToString())),
            RestaurantCreated e => new(new RestaurantCreatedV1(e.RestaurantId.Value, e.PropertyId, ContractValues.Data(e.Details))),
            RestaurantUpdated e => new(new RestaurantUpdatedV1(e.RestaurantId.Value, ContractValues.Data(e.Details))),
            MenuCreated e => new(new MenuCreatedV1(e.MenuId.Value, e.PropertyId, e.RestaurantId.Value, ContractValues.Data(e.Details))),
            MenuUpdated e => new(new MenuUpdatedV1(e.MenuId.Value, ContractValues.Data(e.Details))),
            PropertyEventCreated e => new(new PropertyEventCreatedV1(e.PropertyEventId.Value, e.PropertyId, ContractValues.Data(e.Details), e.Status.ToString())),
            PropertyEventUpdated e => new(new PropertyEventUpdatedV1(e.PropertyEventId.Value, ContractValues.Data(e.Details), e.Status.ToString())),
            ServiceCreated e => new(new ServiceCreatedV1(e.ServiceId.Value, e.PropertyId, ContractValues.Data(e.Details))),
            ServiceUpdated e => new(new ServiceUpdatedV1(e.ServiceId.Value, ContractValues.Data(e.Details))),
            ServiceRequestCreated e => new(new ServiceRequestCreatedV1(e.ServiceRequestId, e.PropertyId, e.ServiceId.Value, e.ServiceCategory.ToString(), ContractValues.Data(e.Details), ContractValues.Data(e.Origin), e.DueAt)),
            ServiceRequestUpdated e => new(new ServiceRequestUpdatedV1(e.ServiceRequestId, e.Status.ToString(), e.ExceptionReason)),
            ServiceRequestCompleted e => new(new ServiceRequestCompletedV1(e.ServiceRequestId, e.PropertyId, ContractValues.Data(e.Origin))),
            HumanActionRequested e => new(new HumanActionRequestedV1(e.HumanActionId.Value, e.PropertyId, e.ServiceRequestId, e.ActionType.ToString(), e.Priority.ToString(), e.Location, e.Deadline, e.SkillsRequired)),
            HumanActionAssigned e => new(new HumanActionAssignedV1(e.HumanActionId.Value, e.StaffMemberId, e.AssignedBy)),
            HumanActionAccepted e => new(new HumanActionAcceptedV1(e.HumanActionId.Value, e.StaffMemberId)),
            HumanActionStarted e => new(new HumanActionStartedV1(e.HumanActionId.Value, e.StaffMemberId, e.ServiceRequestId)),
            HumanActionCompleted e => new(new HumanActionCompletedV1(e.HumanActionId.Value, e.StaffMemberId, e.ServiceRequestId)),
            HumanActionFailed e => new(new HumanActionFailedV1(e.HumanActionId.Value, e.StaffMemberId, e.ServiceRequestId, e.Reason)),
            HumanActionCancelled e => new(new HumanActionCancelledV1(e.HumanActionId.Value, e.ServiceRequestId, e.Reason)),
            HumanActionExpired e => new(new HumanActionExpiredV1(e.HumanActionId.Value, e.ServiceRequestId)),
            StaffMemberCreated e => new(new StaffMemberCreatedV1(e.StaffMemberId, e.PropertyId, e.Details.DisplayName, e.Details.Skills!, e.IsOnDuty)),
            StaffMemberUpdated e => new(new StaffMemberUpdatedV1(e.StaffMemberId, e.Details.DisplayName, e.Details.Skills!)),
            StaffMemberDutyChanged e => new(new StaffMemberDutyChangedV1(e.StaffMemberId, e.PropertyId, e.IsOnDuty)),
            GuestRegistered e => new(new GuestRegisteredV1(e.GuestId)),
            GuestProfileUpdated e => new(new GuestProfileUpdatedV1(e.GuestId)),
            StayContextCreated e => new(new StayContextCreatedV1(e.StayContextId, e.PropertyId, e.GuestId, e.Arrival, e.Departure, e.AccommodationUnitId, e.Status.ToString())),
            StayContextUpdated e => new(new StayContextUpdatedV1(e.StayContextId, e.Arrival, e.Departure, e.AccommodationUnitId, e.Status.ToString())),
            ConversationStarted e => new(new ConversationStartedV1(e.ConversationId, e.PropertyId, e.Channel.ToString())),
            ConversationUpdated e => new(new ConversationUpdatedV1(e.ConversationId, e.GuestId, e.StayContextId, e.Status.ToString())),
            GuestMessageReceived e => new(new GuestMessageReceivedV1(e.GuestMessageId, e.ConversationId, e.PropertyId, e.GuestId, e.StayContextId)),
            GuestMessageSent e => new(new GuestMessageSentV1(e.GuestMessageId, e.ConversationId, e.PropertyId, e.Sender.ToString())),
            GuestMessageTriaged e => new(new GuestMessageTriagedV1(e.GuestMessageId, e.ConversationId, e.PropertyId, e.GuestId, e.StayContextId, e.Intent.ToString())),
            GuestNotificationRequested e => new(new GuestNotificationRequestedV1(e.GuestNotificationId.Value, e.PropertyId, e.ConversationId, e.GuestId, e.Channel.ToString(), e.SubjectType, e.SubjectId)),
            GuestNotificationSent e => new(new GuestNotificationSentV1(e.GuestNotificationId.Value, e.ConversationId, e.ProviderName, e.ProviderMessageId)),
            GuestNotificationFailed e => new(new GuestNotificationFailedV1(e.GuestNotificationId.Value, e.ConversationId, e.Reason)),
            GuestQuestionAnswered e => new(new GuestQuestionAnsweredV1(e.QuestionMessageId, e.AnswerMessageId, e.ConversationId, e.PropertyId, e.Outcome.ToString())),
            CapabilityInvocationRequested e => new(new CapabilityInvocationRequestedV1(e.CapabilityInvocationId.Value, e.CapabilityName, e.CapabilityVersion)),
            CapabilityInvocationSucceeded e => new(new CapabilityInvocationSucceededV1(e.CapabilityInvocationId.Value, e.CapabilityName, e.CapabilityVersion)),
            CapabilityInvocationFailed e => new(new CapabilityInvocationFailedV1(e.CapabilityInvocationId.Value, e.CapabilityName, e.CapabilityVersion, e.ErrorCode)),
            AITraceCreated e => new(new AITraceCreatedV1(e.AITraceId.Value, e.PropertyId, e.Agent, e.TriggerType, e.TriggerId, e.Outcome.ToString())),
            IntegrationConfigurationCreated e => new(new IntegrationConfigurationCreatedV1(e.IntegrationConfigurationId.Value, e.PropertyId, e.Kind.ToString(), e.ProviderName, e.Status.ToString())),
            IntegrationConfigurationUpdated e => new(new IntegrationConfigurationUpdatedV1(e.IntegrationConfigurationId.Value, e.Kind.ToString(), e.ProviderName, e.Status.ToString())),
            ExternalReferenceCreated e => new(new ExternalReferenceCreatedV1(e.ExternalReferenceId.Value, e.PropertyId, e.ProviderName, e.EntityType, e.ExternalId, e.CanonicalId)),
            _ => throw new InvalidOperationException($"{domainEvent.GetType().Name} has no published contract; add one to HospitalityOS.Contracts and map it here."),
        };
    }
}
