using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Content;
using HospitalityOS.Contracts.Api.Content;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Api.Content;

/// <summary>
/// <c>/api/v1/content</c>: the descriptive content of the caller's tenant's properties. Staff read; administrators draft,
/// edit, publish, and archive.
/// </summary>
internal static class ContentEndpoints
{
    public const string Route = "/api/v1/content";

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(StaffPolicies.Staff);

        group.MapPost("/", CreateAsync).Produces<ContentItemResponse>(StatusCodes.Status201Created).WithName(AdministrationActions.ContentItemCreate).RequireAuthorization(StaffPolicies.Admin);
        group.MapGet("/", ListAsync).Produces<IReadOnlyList<ContentItemResponse>>().WithName("ContentItem.List");
        group.MapGet("/{id}", GetAsync).Produces<ContentItemResponse>().WithName("ContentItem.Get");
        group.MapPut("/{id}", UpdateAsync).Produces<ContentItemResponse>().WithName(AdministrationActions.ContentItemUpdate).RequireAuthorization(StaffPolicies.Admin);
        group.MapPost("/{id}/publish", PublishAsync).Produces<ContentItemResponse>().WithName(AdministrationActions.ContentItemPublish).RequireAuthorization(StaffPolicies.Admin);
        group.MapPost("/{id}/archive", ArchiveAsync).Produces<ContentItemResponse>().WithName(AdministrationActions.ContentItemArchive).RequireAuthorization(StaffPolicies.Admin);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateContentItemRequest request, IContentAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Created(await administration.CreateAsync(request, cancellationToken), item => $"{Route}/{item.Id}", item => item.Version);

    /// <summary>
    /// One property's content in every status (<c>?propertyId=</c>, required).
    /// </summary>
    private static async Task<IResult> ListAsync(RecordId propertyId, IContentAdministration administration, CancellationToken cancellationToken) =>
        TypedResults.Ok(await administration.ListAsync(new PropertyId(propertyId.Value), cancellationToken));

    private static async Task<IResult> GetAsync(RecordId id, IContentAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.Ok(await administration.GetAsync(id.Value, cancellationToken), item => item.Version);

    private static Task<IResult> UpdateAsync(RecordId id, UpdateContentItemRequest request, HttpRequest http, IContentAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(
            http,
            expectedVersion => administration.UpdateAsync(id.Value, request, expectedVersion, cancellationToken),
            item => item.Version);

    private static Task<IResult> PublishAsync(RecordId id, HttpRequest http, IContentAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(http, expectedVersion => administration.PublishAsync(id.Value, expectedVersion, cancellationToken), item => item.Version);

    private static Task<IResult> ArchiveAsync(RecordId id, HttpRequest http, IContentAdministration administration, CancellationToken cancellationToken) =>
        VersionedResult.ChangeAsync(http, expectedVersion => administration.ArchiveAsync(id.Value, expectedVersion, cancellationToken), item => item.Version);
}
