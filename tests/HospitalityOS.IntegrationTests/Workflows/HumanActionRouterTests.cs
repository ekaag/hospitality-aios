using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.Workflows.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Workflows;

/// <summary>
/// The HumanAction Router end to end with events dispatched, against the seeded Aurora Grand roster (Jo Okafor and Sam
/// Patel in housekeeping, Ravi Mehta in maintenance, all on duty): new work goes to whoever on duty has its skills and the
/// least open work, by name on a tie, with no manager involved; work nobody can do waits until someone who can joins or
/// comes on duty; someone going off duty hands on what they have not accepted; and work not accepted within the timeout
/// goes to the next person eligible, while accepted work stays put.
/// </summary>
public sealed class HumanActionRouterTests : IAsyncLifetime
{
    private static readonly Actor agent = Actor.Agent("guestservice.request_work");
    private static readonly ActionLocation room412 = new("Room 412", Seed.Room412, Seed.Room412Space);

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Waits until the seeded roster's joining has been handled, so routing in a test is not raced by it.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        Guid[] joined = [.. (await api.EventsAsync(Seed.TenantId, Cancellation)).Where(message => message.EventType == "StaffMemberCreated").Select(message => message.EventId)];
        Assert.Equal(3, joined.Length);

        while (true)
        {
            await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
            {
                IInboxStore inbox = scope.ServiceProvider.GetRequiredService<IInboxStore>();
                bool handled = true;

                foreach (Guid eventId in joined)
                {
                    handled &= await inbox.HasProcessedAsync(DutyRerouting.JoinedName, eventId, timeout.Token);
                }

                if (handled)
                {
                    return;
                }
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task NewWork_GoesToTheLeastLoadedPersonOnDutyWithItsSkills_ByNameOnATie()
    {
        Guid first = await RequestAsync("housekeeping");
        HumanAction toJo = await UntilAsync(first, action => action.AssignedTo is not null);
        Guid second = await RequestAsync("housekeeping");
        Guid repair = await RequestAsync("maintenance");
        Guid spa = await RequestAsync("spa");
        Guid third = await RequestAsync("housekeeping");

        HumanAction toSam = await UntilAsync(second, action => action.AssignedTo is not null);
        HumanAction toRavi = await UntilAsync(repair, action => action.AssignedTo is not null);
        await UntilAsync(third, action => action.AssignedTo is not null);

        Assert.Equal(
            [(HumanActionStatus.Assigned, (StaffMemberId?)Seed.JoOkafor, NewWorkRouting.Name), (HumanActionStatus.Assigned, Seed.SamPatel, NewWorkRouting.Name), (HumanActionStatus.Assigned, Seed.RaviMehta, NewWorkRouting.Name)],
            new[] { toJo, toSam, toRavi }.Select(action => (action.Status, action.AssignedTo, action.Assignments[^1].AssignedBy)));

        // Routing handles requests in order, so once the later one is assigned the spa work was considered and left waiting.
        HumanAction waiting = await LoadAsync(spa);
        Assert.Equal((HumanActionStatus.Requested, (StaffMemberId?)null), (waiting.Status, waiting.AssignedTo));
    }

    [Fact]
    public async Task WorkNobodyCanDo_Waits_UntilSomeoneWhoCanComesOnDutyOrJoinsOnDuty()
    {
        Guid spa = await RequestAsync("spa");
        Guid pool = await RequestAsync("pool");
        await UntilAsync(await RequestAsync("housekeeping"), action => action.AssignedTo is not null);
        using HttpClient admin = api.Staff(Seed.TenantId);
        StaffMemberResponse mia = await JoinAsync(admin, "Mia Chen", "spa", onDuty: false);

        using HttpClient miaClient = api.Staff(Seed.TenantId, "Worker", mia.Id.ToString());
        using HttpResponseMessage onDuty = await miaClient.PutAsJsonAsync($"/api/v1/staff/{mia.Id}/duty", new SetDutyRequest(true), Cancellation);
        await StaffApi.OkAsync<StaffMemberResponse>(onDuty, Cancellation);
        HumanAction toMia = await UntilAsync(spa, action => action.AssignedTo is not null);

        StaffMemberResponse lee = await JoinAsync(admin, "Lee Wong", "pool", onDuty: true);
        HumanAction toLee = await UntilAsync(pool, action => action.AssignedTo is not null);

        Assert.Equal((mia.Id, DutyRerouting.Name), (toMia.AssignedTo!.Value, toMia.Assignments[^1].AssignedBy));
        Assert.Equal((lee.Id, DutyRerouting.JoinedName), (toLee.AssignedTo!.Value, toLee.Assignments[^1].AssignedBy));
    }

    [Fact]
    public async Task GoingOffDuty_HandsOnWorkNotYetAccepted_AndKeepsWorkAccepted()
    {
        Guid jos = await RequestAsync("housekeeping");
        await UntilAsync(jos, action => action.AssignedTo == Seed.JoOkafor);
        Guid sams = await RequestAsync("housekeeping");
        await UntilAsync(sams, action => action.AssignedTo == Seed.SamPatel);
        await SucceedAsync(Worker(Seed.SamPatel), "HumanAction.UpdateStatus", new UpdateActionStatusInput(sams, ActionStatus.Accepted));

        await SetDutyAsync(Seed.JoOkafor, false);
        HumanAction handedOn = await UntilAsync(jos, action => action.AssignedTo == Seed.SamPatel);
        await SetDutyAsync(Seed.SamPatel, false);

        Assert.Equal(DutyRerouting.Name, handedOn.Assignments[^1].AssignedBy);
        Assert.Equal([Seed.JoOkafor, Seed.SamPatel], handedOn.Assignments.Select(assignment => assignment.StaffMemberId));

        // With both housekeepers off duty, nobody can take Jo's old work, and Sam keeps what they accepted.
        await UntilAsync(await RequestAsync("maintenance"), action => action.AssignedTo is not null);
        Assert.Equal((HumanActionStatus.Assigned, (StaffMemberId?)Seed.SamPatel), ((await LoadAsync(jos)).Status, (await LoadAsync(jos)).AssignedTo));
        Assert.Equal((HumanActionStatus.Accepted, (StaffMemberId?)Seed.SamPatel), ((await LoadAsync(sams)).Status, (await LoadAsync(sams)).AssignedTo));
    }

    [Fact]
    public async Task WorkNotAcceptedInTime_GoesToTheNextPersonEligible_WhileAcceptedWorkStays()
    {
        Guid jos = await RequestAsync("housekeeping");
        await UntilAsync(jos, action => action.AssignedTo == Seed.JoOkafor);
        Guid sams = await RequestAsync("housekeeping");
        await UntilAsync(sams, action => action.AssignedTo == Seed.SamPatel);
        await SucceedAsync(Worker(Seed.SamPatel), "HumanAction.UpdateStatus", new UpdateActionStatusInput(sams, ActionStatus.Accepted));

        api.Clock.Advance(new HumanActionRoutingOptions().AcceptanceTimeout);

        HumanAction reassigned = await UntilAsync(jos, action => action.AssignedTo == Seed.SamPatel);
        Assert.Equal((HumanActionStatus.Assigned, UnacceptedRerouting.Name), (reassigned.Status, reassigned.Assignments[^1].AssignedBy));
        HumanAction accepted = await LoadAsync(sams);
        Assert.Equal((HumanActionStatus.Accepted, 1), (accepted.Status, accepted.Assignments.Count));
    }

    [Fact]
    public async Task WorkAcceptedInTime_IsNotReassigned()
    {
        Guid jos = await RequestAsync("housekeeping");
        await UntilAsync(jos, action => action.AssignedTo == Seed.JoOkafor);
        await SucceedAsync(Worker(Seed.JoOkafor), "HumanAction.UpdateStatus", new UpdateActionStatusInput(jos, ActionStatus.Accepted));

        api.Clock.Advance(new HumanActionRoutingOptions().AcceptanceTimeout);
        await UntilAsync(await RequestAsync("maintenance"), action => action.AssignedTo is not null);

        HumanAction kept = await LoadAsync(jos);
        Assert.Equal((HumanActionStatus.Accepted, 1), (kept.Status, kept.Assignments.Count));
    }

    private static Actor Worker(StaffMemberId id) => Actor.Staff(id.ToString(), StaffRole.Worker);

    private static async Task<StaffMemberResponse> JoinAsync(HttpClient admin, string name, string skill, bool onDuty)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            "/api/v1/staff", new CreateStaffMemberRequest(PropertyId: Seed.PropertyId, DisplayName: name, Skills: [skill], IsOnDuty: onDuty), ContractJson.Options, Cancellation);

        return await StaffApi.CreatedAsync<StaffMemberResponse>(response, Cancellation);
    }

    private async Task SetDutyAsync(StaffMemberId id, bool onDuty)
    {
        using HttpClient self = api.Staff(Seed.TenantId, "Worker", id.ToString());
        using HttpResponseMessage response = await self.PutAsJsonAsync($"/api/v1/staff/{id}/duty", new SetDutyRequest(onDuty), Cancellation);
        await StaffApi.OkAsync<StaffMemberResponse>(response, Cancellation);
    }

    private async Task<Guid> RequestAsync(string skill)
    {
        CreateActionInput work = new(
            Seed.PropertyId,
            ActionType.CheckRoom,
            $"Check the {skill} problem",
            room412,
            "Reported by the guest.",
            SkillsRequired: [skill]);

        return (await SucceedAsync(agent, "HumanAction.Create", work, Guid.CreateVersion7().ToString())).Source.Id;
    }

    private async Task<HumanActionEntry> SucceedAsync(Actor actor, string capability, object input, string? key = null)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(actor);
        Result<CapabilityResponse> result = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(capability, JsonSerializer.SerializeToElement(input, ContractJson.Options), IdempotencyKey: key), Cancellation);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

        return result.Value.Output.Deserialize<HumanActionOutput>(ContractJson.Options)!.Action;
    }

    private async Task<HumanAction> LoadAsync(Guid id)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);

        return await scope.ServiceProvider.GetRequiredService<IHumanActionRepository>().GetByIdAsync(new HumanActionId(id), Cancellation)
            ?? throw new Xunit.Sdk.XunitException($"Human action {id} does not exist.");
    }

    /// <summary>
    /// The action once <paramref name="condition"/> holds, waiting for the router.
    /// </summary>
    private async Task<HumanAction> UntilAsync(Guid id, Func<HumanAction, bool> condition)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            HumanAction action = await LoadAsync(id);

            if (condition(action))
            {
                return action;
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
