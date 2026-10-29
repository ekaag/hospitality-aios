using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Bindings;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.Workflows.Concierge;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.AiEvals;

/// <summary>
/// The real API host with the Aurora Grand seeded at <see cref="Now"/> (a Monday, 18:00 in London) and events dispatched,
/// where Alex, in house in room 412, asks the concierge over HTTP. Its models are the Fake model playing the scenario's
/// scripted answer and the Fake judge, unless <see cref="SettingsVariable"/> names a settings file binding
/// <c>concierge.answer</c> and <c>eval.judge</c> to real models (see <c>Settings/anthropic.json</c>).
/// </summary>
public sealed class EvalHost : IAsyncDisposable
{
    /// <summary>
    /// The environment variable naming a JSON settings file with an <c>Inference</c> section to evaluate real models with.
    /// </summary>
    public const string SettingsVariable = "HOSPITALITYOS_EVALS_SETTINGS";

    /// <summary>
    /// The judge's task: an <see cref="IEvaluationJudge"/> call grading each answer.
    /// </summary>
    public const string JudgeTask = "eval.judge";

    private const string Issuer = "https://identity.evals.hospitalityos";
    private const string Audience = "hospitalityos-api";

    private static readonly SymmetricSecurityKey signingKey = new(SHA256.HashData(Encoding.UTF8.GetBytes("hospitalityos-ai-evals")));

    private readonly WebApplicationFactory<Program> factory;

    public EvalHost(EvalScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        string? settings = Environment.GetEnvironmentVariable(SettingsVariable);
        Live = !string.IsNullOrWhiteSpace(settings);
        FakeScriptOptions script = new();

        foreach (FakeChatRuleOptions rule in scenario.Scripted)
        {
            script.Chat.Add(rule);
        }

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:AuroraGrand:Enabled", "true");
            builder.UseSetting("Eventing:Dispatcher:Enabled", "true");
            builder.UseSetting("Eventing:Dispatcher:PollInterval", "00:00:00.050");

            if (Live)
            {
                builder.ConfigureAppConfiguration(configuration => configuration.AddJsonFile(Path.GetFullPath(settings!), optional: false));
            }
            else
            {
                builder.UseSetting("Inference:Providers:scripted:Adapter", "Fake");
                builder.UseSetting("Inference:Providers:scripted:Settings:Script", "concierge");
                builder.UseSetting($"Inference:Tasks:{Concierge.AnswerTask}:Provider", "scripted");
                builder.UseSetting($"Inference:Tasks:{Concierge.AnswerTask}:Model", "fake-concierge");
                builder.UseSetting($"Inference:Tasks:{JudgeTask}:Provider", "fake");
                builder.UseSetting($"Inference:Tasks:{JudgeTask}:Model", "fake-judge");
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddInferenceTask<IEvaluationJudge>(JudgeTask);
                services.Configure<FakeInferenceOptions>(options => options.Scripts["concierge"] = script);
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = Audience;
                    options.TokenValidationParameters.IssuerSigningKey = signingKey;
                });
            });
        });
    }

    /// <summary>
    /// Monday 26 October 2026, 18:00 in London (GMT): before tonight's wine tasting and jazz night.
    /// </summary>
    public static DateTimeOffset Now { get; } = new(2026, 10, 26, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Whether real models are bound (<see cref="SettingsVariable"/> is set).
    /// </summary>
    public bool Live { get; }

    /// <summary>
    /// The host's clock, advanced while waiting for the concierge (the event dispatcher waits on it between polls).
    /// </summary>
    public FakeTimeProvider Clock { get; } = new(Now);

    /// <summary>
    /// A client acting as the property's administrator.
    /// </summary>
    public HttpClient Admin() => Client(new Dictionary<string, object> { ["sub"] = "eval-admin", ["tenant_id"] = Seed.TenantId.Value.ToString(), ["role"] = "Admin" });

    /// <summary>
    /// Asks the concierge as Alex in a new conversation and waits for the answer: about ten seconds with the Fake model,
    /// two minutes with real ones.
    /// </summary>
    public async Task<(GuestMessageResponse Question, GuestMessageResponse Answer)> AskAsync(string text, CancellationToken cancellationToken)
    {
        using HttpClient alex = Client(new Dictionary<string, object>
        {
            ["sub"] = Seed.Alex.Value.ToString(),
            ["tenant_id"] = Seed.TenantId.Value.ToString(),
            ["property_id"] = Seed.PropertyId.Value.ToString(),
            ["stay_id"] = Seed.AlexStay.Value.ToString(),
            ["role"] = "Guest",
        });
        ConversationResponse conversation = await ReadAsync<ConversationResponse>(await alex.PostAsync("/api/v1/conversations", null, cancellationToken), cancellationToken);
        GuestMessageResponse question = await ReadAsync<GuestMessageResponse>(
            await alex.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new { text, clientMessageId = Guid.NewGuid().ToString() }, cancellationToken),
            cancellationToken);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Live ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(10));

        while (true)
        {
            IReadOnlyList<GuestMessageResponse> messages = await ReadAsync<IReadOnlyList<GuestMessageResponse>>(
                await alex.GetAsync($"/api/v1/conversations/{conversation.Id}/messages", timeout.Token), timeout.Token);

            if (messages.Count >= 2)
            {
                return (question, messages[1]);
            }

            Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    /// <summary>
    /// The one trace of the flow, in full (as an administrator reads it), or <c>null</c> when there is none or several.
    /// </summary>
    public async Task<AITraceResponse?> TraceAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        using HttpClient admin = Admin();
        AITracePage page = await ReadAsync<AITracePage>(await admin.GetAsync($"/api/v1/ai/traces?correlationId={correlationId}", cancellationToken), cancellationToken);

        return page.Traces.Count == 1
            ? await ReadAsync<AITraceResponse>(await admin.GetAsync($"/api/v1/ai/traces/{page.Traces[0].Id}", cancellationToken), cancellationToken)
            : null;
    }

    /// <summary>
    /// The events of the property's tenant, in the order they were recorded.
    /// </summary>
    public async Task<IReadOnlyList<EventMessage>> EventsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IReadOnlyList<StoredEvent> stored = await scope.ServiceProvider.GetRequiredService<IEventStore>().ReadAsync(new EventStoreQuery(TenantId: Seed.TenantId), cancellationToken);

        return [.. stored.Select(entry => entry.Message)];
    }

    /// <summary>
    /// Grades an answer with the model bound to <see cref="JudgeTask"/>.
    /// </summary>
    public async Task<Result<Judgement>> JudgeAsync(JudgeRequest request, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
        Result<InferenceOutcome<Judgement>> judged = await scope.ServiceProvider.GetRequiredService<IInferenceRouter>()
            .RunAsync<IEvaluationJudge, Judgement>(JudgeTask, (judge, model, _, ct) => judge.JudgeAsync(request, model, ct), cancellationToken);

        return judged.IsSuccess ? judged.Value.Value : judged.Error;
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();

    /// <summary>
    /// Reads a successful response, or fails with its status and body.
    /// </summary>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"{(int)response.StatusCode} from {response.RequestMessage?.RequestUri}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            }

            return (await response.Content.ReadFromJsonAsync<T>(ContractJson.Options, cancellationToken))!;
        }
    }

    private HttpClient Client(Dictionary<string, object> claims)
    {
        HttpClient client = factory.CreateClient();
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddHours(1),
            Claims = claims,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
