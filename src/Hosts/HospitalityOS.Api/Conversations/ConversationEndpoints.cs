using System.Security.Claims;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Http;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Api.Conversations;

/// <summary>
/// <c>/api/v1/conversations</c>: a guest's chat with the concierge (Journey 5), with a stay token. A guest starts a
/// conversation, posts messages, and reads the messages back; the concierge's answers arrive there asynchronously, once
/// the message's <c>GuestMessageReceived</c> has been handled.
/// </summary>
internal static class ConversationEndpoints
{
    public const string Route = "/api/v1/conversations";

    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(Route).RequireAuthorization(GuestPolicies.Guest);

        group.MapPost("/", StartAsync).Produces<ConversationResponse>(StatusCodes.Status201Created).WithName("Conversation.Start");
        group.MapGet("/{id}", GetAsync).Produces<ConversationResponse>().WithName("Conversation.Get");
        group.MapPost("/{id}/messages", PostMessageAsync).Produces<GuestMessageResponse>().Produces<GuestMessageResponse>(StatusCodes.Status201Created).WithName("Conversation.PostMessage");
        group.MapGet("/{id}/messages", ListMessagesAsync).Produces<IReadOnlyList<GuestMessageResponse>>().WithName("Conversation.ListMessages");

        return endpoints;
    }

    private static async Task<IResult> StartAsync(ClaimsPrincipal user, IGuestConversations conversations, CancellationToken cancellationToken)
    {
        Result<ConversationResponse> started = await conversations.StartAsync(Caller(user), cancellationToken);

        return started.IsSuccess ? TypedResults.Created($"{Route}/{started.Value.Id}", started.Value) : started.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(RecordId id, ClaimsPrincipal user, IGuestConversations conversations, CancellationToken cancellationToken)
    {
        Result<ConversationResponse> conversation = await conversations.GetAsync(Caller(user), id.Value, cancellationToken);

        return conversation.IsSuccess ? TypedResults.Ok(conversation.Value) : conversation.Error.ToProblem();
    }

    /// <summary>
    /// <c>201 Created</c> for a new message; <c>200 OK</c> with the recorded one when its <c>clientMessageId</c> was sent
    /// before.
    /// </summary>
    private static async Task<IResult> PostMessageAsync(
        RecordId id,
        PostMessageRequest request,
        ClaimsPrincipal user,
        IGuestConversations conversations,
        CancellationToken cancellationToken)
    {
        Result<PostedMessage> posted = await conversations.PostMessageAsync(Caller(user), id.Value, request, cancellationToken);

        return posted.IsFailure ? posted.Error.ToProblem()
            : posted.Value.Created ? TypedResults.Created($"{Route}/{id.Value}/messages", posted.Value.Message)
            : TypedResults.Ok(posted.Value.Message);
    }

    private static async Task<IResult> ListMessagesAsync(RecordId id, ClaimsPrincipal user, IGuestConversations conversations, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<GuestMessageResponse>> messages = await conversations.ListMessagesAsync(Caller(user), id.Value, cancellationToken);

        return messages.IsSuccess ? TypedResults.Ok(messages.Value) : messages.Error.ToProblem();
    }

    // The guest policy admitted the caller, and authentication refuses incomplete guest tokens, so the claims are complete.
    private static GuestCaller Caller(ClaimsPrincipal user) =>
        GuestClaims.TryRead(user, out TenantId _, out GuestCaller? caller, out string? problem) ? caller! : throw new InvalidOperationException(problem);
}
