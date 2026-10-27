using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.SharedKernel.Results;
using ContractSubjectKind = HospitalityOS.Contracts.Capabilities.SubjectKind;
using DomainSubjectKind = HospitalityOS.Domain.Properties.Shared.SubjectKind;

namespace HospitalityOS.Capabilities.Properties;

/// <summary>
/// <c>Property.GetOperatingHours</c>: the authoritative hours of the property or one of its amenities, restaurants,
/// spaces, or services on a date, and whether it is open now (Journeys 5 and 16).
/// </summary>
internal sealed class GetOperatingHoursCapability(
    IPropertyRepository properties,
    IOperatingHoursRepository operatingHours,
    TimeProvider timeProvider) : ICapability<GetOperatingHoursInput, GetOperatingHoursOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "Property.GetOperatingHours",
        "Returns the opening hours of the property, or of one amenity, restaurant, space, or service, on a property-local date "
            + "(today by default), including holiday hours and temporary closures, and whether it is open right now. "
            + "A not-found result means no hours are recorded: say they are unknown, never guess.");

    public async Task<Result<GetOperatingHoursOutput>> ExecuteAsync(GetOperatingHoursInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        Guid? subjectId = input.Subject is ContractSubjectKind.Property ? input.PropertyId.Value : input.SubjectId;

        if (subjectId is null || subjectId == Guid.Empty)
        {
            return ReadErrors.SubjectIdRequired;
        }

        Result<PropertyClock> loaded = await PropertyClock.LoadAsync(properties, input.PropertyId, timeProvider, cancellationToken).ConfigureAwait(false);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        PropertyClock clock = loaded.Value;
        SubjectReference subject = new(ContractEnum.To<DomainSubjectKind>(input.Subject), subjectId.Value);
        OperatingHours? hours = await operatingHours.FindBySubjectAsync(input.PropertyId, subject, cancellationToken).ConfigureAwait(false);

        if (hours is null)
        {
            return ReadErrors.HoursNotFound;
        }

        return new GetOperatingHoursOutput(
            new Subject(input.Subject, subjectId.Value),
            clock.Property.Details.TimeZoneId,
            clock.LocalNow,
            Hours.On(hours, input.Date ?? clock.Today, clock));
    }
}
