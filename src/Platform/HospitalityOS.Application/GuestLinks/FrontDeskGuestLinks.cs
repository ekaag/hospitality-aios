using HospitalityOS.Application.Access;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts.Api.Stays;
using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractStatus = HospitalityOS.Contracts.Api.Stays.StayStatus;
using DomainStatus = HospitalityOS.Domain.Guests.StayContexts.StayStatus;

namespace HospitalityOS.Application.GuestLinks;

internal sealed class FrontDeskGuestLinks(
    IStayContextRepository stays,
    IGuestRepository guests,
    IAccommodationUnitRepository units,
    IGuestLinkRepository links,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : IGuestLinks
{
    public static Error StayNotFound { get; } = Error.NotFound("stay.not_found", "There is no such stay.");

    public async Task<IReadOnlyList<StayResponse>> ListStaysAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<StayContext> active = await stays.ListActiveForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);
        HashSet<StayContextId> linked = [.. (await links.ListOpenForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false)).Select(link => link.StayContextId)];
        List<StayResponse> responses = [];

        foreach (StayContext stay in active)
        {
            responses.Add(await ResponseAsync(stay, linked.Contains(stay.Id), cancellationToken).ConfigureAwait(false));
        }

        return responses;
    }

    public async Task<Result<CreatedGuestLink>> CreateAsync(Guid stayId, CancellationToken cancellationToken)
    {
        StayContext? stay = await FindAsync(stayId, cancellationToken).ConfigureAwait(false);

        if (stay is null)
        {
            return await commit.RejectAsync(AdministrationActions.GuestLinkCreate, StayNotFound, Target(stayId, null), cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        (string code, string hash) = InviteCodes.New();
        Result<GuestLink> created = GuestLink.Create(new GuestLinkId(ids.Next()), stay, hash, now);

        if (created.IsSuccess && await links.FindOpenForStayAsync(stay.Id, cancellationToken).ConfigureAwait(false) is { } replaced)
        {
            replaced.Revoke(now);
        }

        return await commit.CreateAsync(
            AdministrationActions.GuestLinkCreate,
            created,
            link => links.AddAsync(link, cancellationToken),
            link => Target(stay.Id.Value, stay.PropertyId),
            _ => new CreatedGuestLink(stay.Id, code),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<StayResponse>> RevokeAsync(Guid stayId, CancellationToken cancellationToken)
    {
        StayContext? stay = await FindAsync(stayId, cancellationToken).ConfigureAwait(false);

        if (stay is null)
        {
            return await commit.RejectAsync(AdministrationActions.GuestLinkRevoke, StayNotFound, Target(stayId, null), cancellationToken).ConfigureAwait(false);
        }

        if (await links.FindOpenForStayAsync(stay.Id, cancellationToken).ConfigureAwait(false) is { } open)
        {
            open.Revoke(timeProvider.GetUtcNow());
        }

        Result committed = await commit.SucceedAsync(AdministrationActions.GuestLinkRevoke, Target(stay.Id.Value, stay.PropertyId), cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error : await ResponseAsync(stay, hasLink: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<StayResponse> ResponseAsync(StayContext stay, bool hasLink, CancellationToken cancellationToken)
    {
        Guest? guest = await guests.GetByIdAsync(stay.GuestId, cancellationToken).ConfigureAwait(false);
        AccommodationUnit? unit = stay.AccommodationUnitId is { } unitId ? await units.GetByIdAsync(unitId, cancellationToken).ConfigureAwait(false) : null;

        return new StayResponse(
            stay.Id,
            stay.GuestId,
            guest?.Profile.DisplayName,
            unit?.UnitNumber,
            stay.Arrival,
            stay.Departure,
            stay.Status == DomainStatus.InHouse ? ContractStatus.InHouse : ContractStatus.Reserved,
            hasLink);
    }

    // An empty GUID cannot be a stay ID, so it names nothing.
    private Task<StayContext?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<StayContext?>(null) : stays.GetByIdAsync(new StayContextId(id), cancellationToken);

    private static AuditTarget Target(Guid stayId, PropertyId? propertyId) => new(nameof(StayContext), stayId.ToString(), propertyId);
}
