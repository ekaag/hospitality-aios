using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Hours;

internal sealed class OperatingHoursAdministration(
    IOperatingHoursRepository operatingHours,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IOperatingHoursAdministration
{
    public async Task<Result<OperatingHoursResponse>> CreateAsync(CreateOperatingHoursRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "hours", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.OperatingHoursCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        SubjectReference? subject = await references.SubjectAsync(property.Value, request.Subject, "subject", "hours.subject", fields, cancellationToken).ConfigureAwait(false);

        if (subject is null)
        {
            fields.Add("subject", "hours.subject_required", "subject is required.");
        }
        else if (!fields.HasErrors && await operatingHours.FindBySubjectAsync(property.Value, subject, cancellationToken).ConfigureAwait(false) is not null)
        {
            return await commit.RejectAsync(AdministrationActions.OperatingHoursCreate, AdministrationErrors.OperatingHoursAlreadySet, null, cancellationToken).ConfigureAwait(false);
        }

        OperatingHoursId id = new(idGenerators.Resolve().NewId());

        // Without a subject the domain cannot validate the rest, so a placeholder stands in: the request is rejected anyway.
        Result<OperatingHours> created = fields.Merge(OperatingHours.Create(
            id,
            currentTenant.TenantId,
            property.Value,
            subject ?? new SubjectReference(SubjectKind.Property, property.Value.Value),
            Schedule(request.Weekly, request.Overrides, request.Closures),
            timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.OperatingHoursCreate,
            created,
            hours => operatingHours.AddAsync(hours, cancellationToken),
            hours => Target(hours.Id.Value, hours.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<OperatingHoursResponse>> UpdateAsync(Guid id, UpdateOperatingHoursRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        OperatingHours? hours = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            AdministrationActions.OperatingHoursUpdate,
            Target(id, hours?.PropertyId),
            hours,
            AdministrationErrors.OperatingHoursNotFound,
            expectedVersion,
            found => Task.FromResult(found.Update(Schedule(request.Weekly, request.Overrides, request.Closures), timeProvider.GetUtcNow())),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<OperatingHoursResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        OperatingHours? hours = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return hours is null ? AdministrationErrors.OperatingHoursNotFound : Response(hours);
    }

    public async Task<IReadOnlyList<OperatingHoursResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OperatingHours> found = await operatingHours.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(hours => hours.Subject.Kind).ThenBy(hours => hours.Subject.Id).Select(Response)];
    }

    // An empty GUID cannot be an operating hours ID, so it names no record.
    private Task<OperatingHours?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<OperatingHours?>(null) : operatingHours.GetByIdAsync(new OperatingHoursId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(OperatingHours), id.ToString(), propertyId);

    // Entries missing a required value become null entries, which the domain reports by position (e.g. weekly[2]
    // "hours.day_required"); an interval missing a time is reported as invalid.
    private static OperatingHoursSchedule Schedule(ValueList<WeeklyHours>? weekly, ValueList<HoursOverride>? overrides, ValueList<HoursClosure>? closures) =>
        new(
            ValueList.From((weekly ?? []).Select(day => day is { Day: { } dayOfWeek } ? new DayHours(dayOfWeek, Intervals(day.Intervals)) : null!)),
            ValueList.From((overrides ?? []).Select(entry => entry is { Date: { } date } ? new DateOverride(date, Intervals(entry.Intervals), entry.Note) : null!)),
            ValueList.From((closures ?? []).Select(closure => closure is { From: { } from, To: { } to } ? new TemporaryClosure(from, to, closure.Reason) : null!)));

    private static ValueList<TimeInterval> Intervals(ValueList<OpeningInterval>? intervals) =>
        ValueList.From((intervals ?? []).Select(interval => interval is { Opens: { } opens, Closes: { } closes } ? new TimeInterval(opens, closes) : null!));

    private static OperatingHoursResponse Response(OperatingHours hours)
    {
        OperatingHoursSchedule schedule = hours.Schedule;

        return new OperatingHoursResponse(
            hours.Id.Value,
            hours.PropertyId,
            Subjects.ToContract(hours.Subject),
            ValueList.From(schedule.Weekly.Select(day => new WeeklyHours(day.Day, Intervals(day.Intervals)))),
            ValueList.From(schedule.Overrides.Select(entry => new HoursOverride(entry.Date, Intervals(entry.Intervals), entry.Note))),
            ValueList.From(schedule.Closures.Select(closure => new HoursClosure(closure.From, closure.To, closure.Reason))),
            hours.Version);
    }

    private static ValueList<OpeningInterval> Intervals(ValueList<TimeInterval> intervals) =>
        ValueList.From(intervals.Select(interval => new OpeningInterval(interval.Opens, interval.Closes)));
}
