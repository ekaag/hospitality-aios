using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.Workflows.Concierge;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Workflows;

/// <summary>
/// A guest chatting with the concierge in the real host over HTTP, with events dispatched and the Fake model following a
/// script, and what a manager then sees of the turn.
/// </summary>
internal static class GuestChat
{
    public const string Route = "/api/v1/conversations";

    /// <summary>
    /// A host whose <c>concierge.answer</c> task runs on the Fake model with <paramref name="script"/>.
    /// </summary>
    public static StaffApi Host(FakeScriptOptions script, Action<IWebHostBuilder>? configure = null, DateTimeOffset? now = null) =>
        new(dispatchEvents: true, now: now, configure: builder =>
        {
            builder.UseSetting("Inference:Providers:scripted:Adapter", "Fake");
            builder.UseSetting("Inference:Providers:scripted:Settings:Script", "concierge");
            builder.UseSetting($"Inference:Tasks:{Concierge.AnswerTask}:Provider", "scripted");
            builder.UseSetting($"Inference:Tasks:{Concierge.AnswerTask}:Model", "fake-concierge");
            builder.ConfigureTestServices(services => services.Configure<FakeInferenceOptions>(options => options.Scripts["concierge"] = script));
            configure?.Invoke(builder);
        });

    /// <summary>
    /// The record as read capabilities cite it.
    /// </summary>
    public static SourceReference Source<TId>(AggregateRoot<TId> record)
        where TId : struct, IEquatable<TId>, ITypedId =>
        new(record.GetType().Name, record.Id.Value, record.Version);

    public static async Task<ConversationResponse> StartAsync(HttpClient guest, CancellationToken cancellationToken) =>
        await StaffApi.CreatedAsync<ConversationResponse>(await guest.PostAsync(Route, null, cancellationToken), cancellationToken);

    public static async Task<GuestMessageResponse> PostAsync(HttpClient guest, ConversationResponse conversation, string text, CancellationToken cancellationToken) =>
        await StaffApi.CreatedAsync<GuestMessageResponse>(
            await guest.PostAsJsonAsync($"{Route}/{conversation.Id}/messages", new { text, clientMessageId = Guid.NewGuid().ToString() }, cancellationToken),
            cancellationToken);

    /// <summary>
    /// The concierge answers asynchronously: reads again until the conversation has <paramref name="count"/> messages, for
    /// at most ten seconds.
    /// </summary>
    public static async Task<IReadOnlyList<GuestMessageResponse>> MessagesUntilAsync(
        StaffApi host,
        HttpClient guest,
        ConversationResponse conversation,
        int count,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            IReadOnlyList<GuestMessageResponse> messages = await StaffApi.OkAsync<IReadOnlyList<GuestMessageResponse>>(
                await guest.GetAsync($"{Route}/{conversation.Id}/messages", timeout.Token), timeout.Token);

            if (messages.Count >= count)
            {
                return messages;
            }

            host.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    /// <summary>
    /// Asks a question in a new conversation and waits for the concierge's answer.
    /// </summary>
    public static async Task<(GuestMessageResponse Question, GuestMessageResponse Answer)> AskAsync(
        StaffApi host,
        HttpClient guest,
        string question,
        CancellationToken cancellationToken)
    {
        ConversationResponse conversation = await StartAsync(guest, cancellationToken);
        GuestMessageResponse asked = await PostAsync(guest, conversation, question, cancellationToken);
        IReadOnlyList<GuestMessageResponse> messages = await MessagesUntilAsync(host, guest, conversation, 2, cancellationToken);

        return (asked, messages[1]);
    }

    /// <summary>
    /// The one trace of a flow, in full, as a manager reads it.
    /// </summary>
    public static async Task<AITraceResponse> TraceAsync(StaffApi host, TenantId tenant, Guid correlationId, CancellationToken cancellationToken)
    {
        using HttpClient manager = host.Staff(tenant, "Manager");
        AITracePage page = await StaffApi.OkAsync<AITracePage>(await manager.GetAsync($"/api/v1/ai/traces?correlationId={correlationId}", cancellationToken), cancellationToken);

        return await StaffApi.OkAsync<AITraceResponse>(await manager.GetAsync($"/api/v1/ai/traces/{Assert.Single(page.Traces).Id}", cancellationToken), cancellationToken);
    }

    /// <summary>
    /// The <c>GuestQuestionAnswered</c> event for a question.
    /// </summary>
    public static async Task<EventMessage> AnsweredAsync(StaffApi host, TenantId tenant, GuestMessageResponse question, CancellationToken cancellationToken) =>
        Assert.Single(await host.EventsAsync(tenant, cancellationToken), message => message.EventType == "GuestQuestionAnswered" && Payload(message).Question == question.Id.Value);

    public static (Guid Question, Guid Answer, string? Outcome) Payload(EventMessage answered)
    {
        using JsonDocument envelope = JsonDocument.Parse(answered.Envelope);
        JsonElement payload = envelope.RootElement.GetProperty("payload");

        return (payload.GetProperty("questionMessageId").GetGuid(), payload.GetProperty("answerMessageId").GetGuid(), payload.GetProperty("outcome").GetString());
    }
}
