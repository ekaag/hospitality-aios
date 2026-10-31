using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Staff;

internal sealed class StaffAdministration(
    IStaffMemberRepository staff,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    OperationContext operation,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : IStaffAdministration
{
    public async Task<Result<StaffMemberResponse>> CreateAsync(CreateStaffMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "staff_member", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.StaffMemberCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        StaffMemberId id = request.Id ?? new StaffMemberId(ids.Next());

        // IDs are unique across the platform, so one taken in this tenant is the only one the caller can collide with.
        if (request.Id is not null && await staff.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is not null)
        {
            fields.Add("id", "staff_member.id_taken", "Someone with this ID is already on a roster.");
        }

        Result<StaffMember> created = fields.Merge(StaffMember.Create(
            id, currentTenant.TenantId, property.Value, new StaffMemberDetails(request.DisplayName!, request.Skills), request.IsOnDuty ?? false, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.StaffMemberCreate,
            created,
            member => staff.AddAsync(member, cancellationToken),
            member => Target(member.Id.Value, member.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StaffMemberResponse>> UpdateAsync(Guid id, UpdateStaffMemberRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        StaffMember? member = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            AdministrationActions.StaffMemberUpdate,
            Target(id, member?.PropertyId),
            member,
            AdministrationErrors.StaffMemberNotFound,
            expectedVersion,
            found => Task.FromResult(found.Update(new StaffMemberDetails(request.DisplayName!, request.Skills), timeProvider.GetUtcNow())),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StaffMemberResponse>> SetDutyAsync(Guid id, SetDutyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        StaffMember? member = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        Actor actor = operation.Actor;
        bool oversees = actor.Role is StaffRole.Manager or StaffRole.Admin;

        return await commit.ChangeAsync(
            AdministrationActions.StaffMemberSetDuty,
            Target(id, member?.PropertyId),
            member,
            AdministrationErrors.StaffMemberNotFound,
            null,
            found => Task.FromResult(
                !oversees && actor.Id != found.Id.ToString() ? AdministrationErrors.DutyForbidden
                : request.IsOnDuty is not { } onDuty ? Error.Validation("isOnDuty", "staff_member.is_on_duty_required", "isOnDuty is required.")
                : found.SetOnDuty(onDuty, timeProvider.GetUtcNow())),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StaffMemberResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        StaffMember? member = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return member is null ? AdministrationErrors.StaffMemberNotFound : Response(member);
    }

    public async Task<IReadOnlyList<StaffMemberResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<StaffMember> found = await staff.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(member => member.Details.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(member => member.Id.Value).Select(Response)];
    }

    // An empty GUID cannot be a staff member ID, so it names nobody.
    private Task<StaffMember?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<StaffMember?>(null) : staff.GetByIdAsync(new StaffMemberId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(StaffMember), id.ToString(), propertyId);

    private static StaffMemberResponse Response(StaffMember member) =>
        new(member.Id, member.PropertyId, member.Details.DisplayName, member.Details.Skills!, member.IsOnDuty, member.Version);
}
