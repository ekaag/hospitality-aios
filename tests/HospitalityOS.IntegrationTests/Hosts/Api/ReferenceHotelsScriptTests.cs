using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Api.ServiceRequests;
using HospitalityOS.Workflows.Concierge;
using HospitalityOS.Workflows.Routing;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using static HospitalityOS.IntegrationTests.Workflows.GuestChat;
using Mysore = HospitalityOS.Seeding.MysoreGrand;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The Fake models' local script (<c>reference-hotels</c>, J3), as the launch profile binds it: at each seeded hotel the
/// concierge answers each question it knows from that hotel's records, citing them as retrieved (an answer citing
/// anything else would be refused), says it does not know anything else, and requests become requests for the guest's
/// room: Alex's at the Aurora Grand, Ananya's at the Mysore Grand.
/// </summary>
public sealed class ReferenceHotelsScriptTests : IAsyncLifetime
{
    private static readonly GuestStay alex = new(Seed.Alex, Seed.AlexStay, Seed.PropertyId);
    private static readonly GuestStay ananya = new(Mysore.Ananya, Mysore.AnanyaStay, Mysore.PropertyId);

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true, configure: Scripted);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    /// <summary>
    /// The launch profile's binding: the <c>fake</c> provider, which every task uses locally, plays the script.
    /// </summary>
    internal static void Scripted(IWebHostBuilder builder) => builder.UseSetting("Inference:Providers:fake:Settings:Script", "reference-hotels");

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

        Assert.Empty(await WrongAnswersAsync(guest, asked));
    }

    [Fact]
    public async Task AtTheMysoreGrand_TheConciergeAnswersFromItsOwnRecords()
    {
        (string Question, string Answer)[] asked =
        [
            ("What time does the pool close?", "The courtyard pool is open daily from 06:30 to 20:00."),
            ("When does the spa open?", "The Ayurveda spa is open daily from 09:00 to 21:00."),
            ("Can my kid swim on their own?", "Children under 14 must be supervised"),
            ("What time is check-out?", "Check-in is from 14:00 and check-out is by 12:00."),
            ("Can I bring my dog?", "Pets are not allowed at the Mysore Grand"),
            ("What should I wear to the music evening?", "Smart casual in Durbar Hall after 19:00"),
            ("What's the wifi password?", "Join the MysoreGrand-Guest network"),
            ("When is the palace lit up?", "nearly 100,000 bulbs"),
            ("Any vegetarian food?", "Mysore masala dosa (INR 280)"),
            ("Where can we have dinner?", "Ambari serves Mysore and Karnataka classics"),
            ("Anything on tonight?", "palace illumination walk from 18:45"),
            ("Can I fly a drone from the roof?", Concierge.NoSourceAnswer),
        ];
        using HttpClient guest = api.Guest(Seed.TenantId, ananya);

        Assert.Empty(await WrongAnswersAsync(guest, asked));
    }

    [Fact]
    public async Task AtTheMysoreGrand_ARequest_BecomesARequestForAnanyasRoom()
    {
        using HttpClient guest = api.Guest(Seed.TenantId, ananya);

        (_, GuestMessageResponse answer) = await AskAsync(api, guest, "Could I get two towels please?", Cancellation);

        Assert.Equal("Of course. I've asked our team to bring 2 bath towels to Room 210.", answer.Text);
        GuestRequestResponse request = Assert.Single(await StaffApi.OkAsync<IReadOnlyList<GuestRequestResponse>>(
            await guest.GetAsync("/api/v1/service-requests", Cancellation), Cancellation));
        Assert.Equal(("Bath towel", 2, "Room 210"), (request.ItemName, request.Quantity, request.Location));
    }

    private async Task<List<string>> WrongAnswersAsync(HttpClient guest, (string Question, string Answer)[] asked)
    {
        List<string> wrong = [];

        foreach ((string question, string expected) in asked)
        {
            (_, GuestMessageResponse answer) = await AskAsync(api, guest, question, Cancellation);

            if (answer.Sender != MessageSender.Concierge || !answer.Text.Contains(expected, StringComparison.Ordinal))
            {
                wrong.Add($"{question} -> {answer.Sender}: {answer.Text}");
            }
        }

        return wrong;
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
