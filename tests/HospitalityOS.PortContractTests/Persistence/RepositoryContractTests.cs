using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.PortContractTests.Persistence;

/// <summary>
/// What every repository and the unit of work guarantee for every aggregate: round-trips, events, versions,
/// tenant isolation (P10), optimistic concurrency, and all-or-nothing commits.
/// </summary>
public abstract class RepositoryContractTests : PersistenceContractTests
{
    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Commit_NewAggregate_LoadsInAnotherScopeWithSameState(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        IAggregateRoot created = aggregateCase.Create(TestData.TenantA, 1);
        await SaveAsync(created);

        IAggregateRoot? loaded = await LoadAsync(aggregateCase, aggregateCase.IdOf(created));

        Assert.NotNull(loaded);
        Assert.NotSame(created, loaded);
        Assert.Equal(AggregateState.Of(created), AggregateState.Of(loaded));
        Assert.Equal(1, loaded.Version);
        Assert.Empty(loaded.DomainEvents);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Commit_NewAggregate_ReturnsItsEventsAndClearsThem(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        IAggregateRoot created = aggregateCase.Create(TestData.TenantA, 1);
        IDomainEvent raised = Assert.Single(created.DomainEvents);
        await using PersistenceSession session = OpenSession();
        await aggregateCase.AddAsync(session, created, Cancellation);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.True(commit.IsSuccess);
        Assert.Equal(
            [new CommittedEvent(TestData.TenantA, aggregate, aggregateCase.IdOf(created), 1, raised)],
            commit.Value.Events);
        Assert.Empty(created.DomainEvents);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Commit_ChangedAggregate_SavesChangeAndAdvancesVersion(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);

        if (!aggregateCase.CanChange)
        {
            return;
        }

        Guid id = await SeedAsync(aggregateCase);
        IAggregateRoot changed;
        CommitReceipt receipt;

        await using (PersistenceSession session = OpenSession())
        {
            changed = (await aggregateCase.GetAsync(session, id, Cancellation))!;
            aggregateCase.Change(changed);
            IDomainEvent raised = Assert.Single(changed.DomainEvents);
            receipt = (await session.CommitAsync(Cancellation)).Value;
            Assert.Equal([new CommittedEvent(TestData.TenantA, aggregate, id, 2, raised)], receipt.Events);
        }

        IAggregateRoot? loaded = await LoadAsync(aggregateCase, id);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Version);
        Assert.Equal(AggregateState.Of(changed), AggregateState.Of(loaded));
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Commit_WithoutChanges_SavesNothing(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        Guid id = await SeedAsync(aggregateCase);
        await using PersistenceSession session = OpenSession();
        await aggregateCase.GetAsync(session, id, Cancellation);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.True(commit.IsSuccess);
        Assert.Empty(commit.Value.Events);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Commit_Twice_SavesEventsOnce(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        await using PersistenceSession session = OpenSession();
        await aggregateCase.AddAsync(session, aggregateCase.Create(TestData.TenantA, 1), Cancellation);
        await session.CommitAsync(Cancellation);

        Result<CommitReceipt> second = await session.CommitAsync(Cancellation);

        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value.Events);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task GetById_InAnotherTenant_ReturnsNull(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        Guid id = await SeedAsync(aggregateCase);
        await using PersistenceSession otherTenant = OpenSession(TestData.TenantB);

        IAggregateRoot? loaded = await aggregateCase.GetAsync(otherTenant, id, Cancellation);

        Assert.Null(loaded);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task GetById_Unknown_ReturnsNull(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);

        IAggregateRoot? loaded = await LoadAsync(aggregateCase, TestData.Id(999_999));

        Assert.Null(loaded);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task GetById_TwiceInOneScope_ReturnsSameInstance(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        Guid id = await SeedAsync(aggregateCase);
        await using PersistenceSession session = OpenSession();

        IAggregateRoot? first = await aggregateCase.GetAsync(session, id, Cancellation);
        IAggregateRoot? second = await aggregateCase.GetAsync(session, id, Cancellation);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Add_WithoutCommit_IsNotSaved(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        IAggregateRoot created = aggregateCase.Create(TestData.TenantA, 1);

        await using (PersistenceSession session = OpenSession())
        {
            await aggregateCase.AddAsync(session, created, Cancellation);
        }

        Assert.Null(await LoadAsync(aggregateCase, aggregateCase.IdOf(created)));
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Change_WithoutCommit_IsNotVisibleToOtherScopes(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);

        if (!aggregateCase.CanChange)
        {
            return;
        }

        Guid id = await SeedAsync(aggregateCase);
        await using PersistenceSession changing = OpenSession();
        aggregateCase.Change((await aggregateCase.GetAsync(changing, id, Cancellation))!);

        IAggregateRoot? loaded = await LoadAsync(aggregateCase, id);

        Assert.Equal(1, loaded!.Version);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Add_AggregateOfAnotherTenant_Throws(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        await using PersistenceSession session = OpenSession(TestData.TenantB);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => aggregateCase.AddAsync(session, aggregateCase.Create(TestData.TenantA, 1), Cancellation));
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Commit_SameIdAddedTwice_ReturnsDuplicateKey(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);
        await SeedAsync(aggregateCase);
        await using PersistenceSession session = OpenSession();
        await aggregateCase.AddAsync(session, aggregateCase.Create(TestData.TenantA, 1), Cancellation);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        AssertConflict(PersistenceErrors.DuplicateKeyCode, commit);
    }

    [Theory]
    [MemberData(nameof(AggregateCases.Names), MemberType = typeof(AggregateCases))]
    public async Task Commit_AfterAnotherScopeSavedFirst_ReturnsConcurrencyConflictAndKeepsEvents(string aggregate)
    {
        AggregateCase aggregateCase = AggregateCases.Get(aggregate);

        if (!aggregateCase.CanChange)
        {
            return;
        }

        Guid id = await SeedAsync(aggregateCase);
        await using PersistenceSession first = OpenSession();
        await using PersistenceSession second = OpenSession();
        IAggregateRoot firstCopy = (await aggregateCase.GetAsync(first, id, Cancellation))!;
        IAggregateRoot secondCopy = (await aggregateCase.GetAsync(second, id, Cancellation))!;
        aggregateCase.Change(firstCopy);
        aggregateCase.Change(secondCopy);
        Assert.True((await first.CommitAsync(Cancellation)).IsSuccess);

        Result<CommitReceipt> commit = await second.CommitAsync(Cancellation);

        AssertConflict(PersistenceErrors.ConcurrencyConflictCode, commit);
        Assert.Single(secondCopy.DomainEvents);
        Assert.Equal(AggregateState.Of(firstCopy), AggregateState.Of((await LoadAsync(aggregateCase, id))!));
    }

    [Fact]
    public async Task Commit_WhenOneAggregateConflicts_SavesNothing()
    {
        AggregateCase organizations = AggregateCases.Get("Organization");
        AggregateCase guests = AggregateCases.Get("Guest");
        Guid id = await SeedAsync(organizations);
        await using PersistenceSession loser = OpenSession();
        IAggregateRoot newGuest = guests.Create(TestData.TenantA, 1);
        await guests.AddAsync(loser, newGuest, Cancellation);
        IAggregateRoot stale = (await organizations.GetAsync(loser, id, Cancellation))!;
        organizations.Change(stale);

        await using (PersistenceSession winner = OpenSession())
        {
            organizations.Change((await organizations.GetAsync(winner, id, Cancellation))!);
            Assert.True((await winner.CommitAsync(Cancellation)).IsSuccess);
        }

        Result<CommitReceipt> commit = await loser.CommitAsync(Cancellation);

        AssertConflict(PersistenceErrors.ConcurrencyConflictCode, commit);
        Assert.Null(await LoadAsync(guests, guests.IdOf(newGuest)));
        Assert.Single(newGuest.DomainEvents);
    }

    [Fact]
    public async Task Commit_SeveralAggregates_ReturnsEventsPerAggregateInOrder()
    {
        AggregateCase guests = AggregateCases.Get("Guest");
        AggregateCase organizations = AggregateCases.Get("Organization");
        IAggregateRoot guest = guests.Create(TestData.TenantA, 1);
        guests.Change(guest);
        IAggregateRoot organization = organizations.Create(TestData.TenantA, 1);
        IDomainEvent[] expected = [.. guest.DomainEvents, .. organization.DomainEvents];
        await using PersistenceSession session = OpenSession();
        await guests.AddAsync(session, guest, Cancellation);
        await organizations.AddAsync(session, organization, Cancellation);

        Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);

        Assert.Equal(expected, commit.Value.Events.Select(committed => committed.Event));
        Assert.Equal([1L, 2L, 1L], commit.Value.Events.Select(committed => committed.AggregateVersion));
    }

    private static void AssertConflict(string code, Result<CommitReceipt> commit)
    {
        Assert.True(commit.IsFailure);
        Assert.Equal(ErrorKind.Conflict, commit.Error.Kind);
        Assert.Equal(code, commit.Error.Code);
    }

    private async Task<Guid> SeedAsync(AggregateCase aggregateCase)
    {
        IAggregateRoot aggregate = aggregateCase.Create(TestData.TenantA, 1);
        await SaveAsync(aggregate);

        return aggregateCase.IdOf(aggregate);
    }

    private async Task<IAggregateRoot?> LoadAsync(AggregateCase aggregateCase, Guid id)
    {
        await using PersistenceSession session = OpenSession();

        return await aggregateCase.GetAsync(session, id, Cancellation);
    }
}
