using HospitalityOS.Application.Administration;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using Xunit;

namespace HospitalityOS.UnitTests.Application;

public sealed class RecordIdsTests
{
    private static readonly Guid first = Guid.Parse("aa000000-0000-4000-8000-000000000001");
    private static readonly Guid second = Guid.Parse("aa000000-0000-4000-8000-000000000002");

    [Fact]
    public void Next_TakesReservedIDsInOrder_ThenNewOnes()
    {
        RecordIds ids = new(new FixedResolver());
        ids.Reserve([first, second]);

        Guid[] taken = [ids.Next(), ids.Next(), ids.Next()];

        Assert.Equal(first, taken[0]);
        Assert.Equal(second, taken[1]);
        Assert.DoesNotContain(taken[2], new[] { first, second, Guid.Empty });
        Assert.Equal(0, ids.Unused);
    }

    [Fact]
    public void Clear_DropsReservedIDsNotTaken()
    {
        RecordIds ids = new(new FixedResolver());
        ids.Reserve([first, second]);
        ids.Next();

        ids.Clear();

        Assert.Equal(0, ids.Unused);
        Assert.NotEqual(second, ids.Next());
    }

    private sealed class FixedResolver : IIdGenerator, IBindingResolver<IIdGenerator>
    {
        public Guid NewId() => Guid.CreateVersion7();

        public IIdGenerator Resolve(BindingContext context) => this;
    }
}
