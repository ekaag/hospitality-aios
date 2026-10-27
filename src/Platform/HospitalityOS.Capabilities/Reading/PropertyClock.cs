using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Reading;

/// <summary>
/// A property with "now" in its own time zone: hours, policies, and events are property-local, so every read that
/// depends on today or now starts here.
/// </summary>
internal sealed record PropertyClock(Property Property, TimeZoneInfo TimeZone, DateTimeOffset Now)
{
    public DateTime LocalNow => TimeZoneInfo.ConvertTime(Now, TimeZone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public static async Task<Result<PropertyClock>> LoadAsync(
        IPropertyRepository properties,
        PropertyId propertyId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Property? property = await properties.GetByIdAsync(propertyId, cancellationToken).ConfigureAwait(false);

        if (property is null)
        {
            return ReadErrors.PropertyNotFound;
        }

        // The time zone ID was validated when the property was saved.
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.Details.TimeZoneId);

        return new PropertyClock(property, timeZone, timeProvider.GetUtcNow());
    }
}
