using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Staff;
using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Staff;

/// <summary>
/// <c>/api/v1/staff</c>: who does physical work at the caller's tenant's properties, which the HumanAction Router routes
/// by. Staff read and put themselves on or off duty; managers put anyone; administrators keep the roster.
/// </summary>
internal static class StaffEndpoints
{
    public const string Route = "/api/v1/staff";

    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).Produces<StaffMemberResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.StaffMemberCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<StaffMemberResponse>>().WithName("StaffMember.List");
        group.MapGet("/{id}", GetAsync).Produces<StaffMemberResponse>().WithName("StaffMember.Get");
        group.MapPut("/{id}", UpdateAsync).Produces<StaffMemberResponse>().WithName(AdministrationActions.StaffMemberUpdate).RequireAuthorization(StaffPolicies.Admin);
        group.MapPut("/{id}/duty", SetDutyAsync).Produces<StaffMemberResponse>().WithName(AdministrationActions.StaffMemberSetDuty);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateStaffMemberRequest request, IStaffAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), member => $"{Route}/{member.Id}", member => member.Version);

    /// <summary>
    /// One property's roster (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IStaffAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IStaffAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), member => member.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateStaffMemberRequest request, HttpRequest http, IStaffAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            member => member.Version);

    private static async Task<IResult> SetDutyAsync(RecordId id, SetDutyRequest request, IStaffAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.SetDutyAsync(id.Value, request, cancellationToken), member => member.Version);
}
