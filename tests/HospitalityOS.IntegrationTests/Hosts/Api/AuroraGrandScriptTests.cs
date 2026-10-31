using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Api.ServiceRequests;
using HospitalityOS.Workflows.Concierge;
using HospitalityOS.Workflows.Routing;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using static HospitalityOS.IntegrationTests.Workflows.GuestChat;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The Fake models' local script (<c>aurora-grand</c>, J3), as the launch profile binds it: against the seeded Aurora Grand
/// the concierge answers each question it knows from the seed's records, citing them as retrieved (an answer citing
/// anything else would be refused), says it does not know anything else, and requests become requests for Alex's room.
/// </summary>
public sealed class AuroraGrandScriptTests : IAsyncLifetime
{
    private static readonly GuestStay alex = new(Seed.Alex, Seed.AlexStay, Seed.PropertyId);

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true, configure: Scripted);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    /// <summary>
    /// The launch profile's binding: the <c>fake</c> provider, which every task uses locally, plays the script.
    /// </summary>
    internal static void Scripted(IWebHostBuilder builder) => builder.UseSetting("Inference:Providers:fake:Settings:Script", "aurora-grand");

    [Fact]
    public async Task TheConcierge_AnswersWhatTheSeedKnows_AndNothingElse()
    {
        (string Question, string Answer)[] asked =
        [
            ("What time does the pool close?", "The pool is open daily from 07:00 to 22:00."),
            ("When is the gym open?", "The gym is open daily from 06:00 to 23:00."),
            ("Can my 12-year-old use the gym?", "Children aged 12 to 15 may use the gym with an adult"),
            ("What time is check-out?", "Check-in is from 15:00 and check-out is by 11:00."),
            ("Can I bring my dog?", "Dogs up to 15 kg are welcome"),
            ("Can I smoke on the balcony?", "non-smoking throughout"),
            ("What's the wifi password?", "Join the Aurora-Guest network"),
            ("Is there parking?", "Valet parking is at the Savoy Street entrance"),
            ("Any vegetarian food?", "paneer tikka (GBP 12.50)"),
            ("Where can we have dinner?", "Saffron serves modern Indian food at dinner"),
            ("Anything on tonight?", "Jazz night is on in the lobby bar"),
            ("Can I fly a drone from the roof?", Concierge.NoSourceAnswer),
        ];
        using HttpClient guest = api.Guest(Seed.TenantId, alex);
        List<string> wrong = [];

        foreach ((string question, string expected) in asked)
        {
            (_, GuestMessageResponse answer) = await AskAsync(api, guest, question, Cancellation);

            if (answer.Sender != MessageSender.Concierge || !answer.Text.Contains(expected, StringComparison.Ordinal))
            {
                wrong.Add($"{question} -> {answer.Sender}: {answer.Text}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public async Task ARequest_BecomesARequestForAlexsRoom()
    {
        using HttpClient guest = api.Guest(Seed.TenantId, alex);

        (_, GuestMessageResponse answer) = await AskAsync(api, guest, "Could I get two towels please?", Cancellation);

        Assert.Equal("Of course. I've asked our team to bring 2 bath towels to Room 412.", answer.Text);
        GuestRequestResponse request = Assert.Single(await StaffApi.OkAsync<IReadOnlyList<GuestRequestResponse>>(
            await guest.GetAsync("/api/v1/service-requests", Cancellation), Cancellation));
        Assert.Equal(("Bath towel", 2, "Room 412"), (request.ItemName, request.Quantity, request.Location));
    }
}
