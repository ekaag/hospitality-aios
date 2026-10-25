using HospitalityOS.SharedKernel.Collections;
using Xunit;

namespace HospitalityOS.UnitTests.SharedKernel.Collections;

public sealed class ValueListTests
{
    private sealed record Holder(string Name, ValueList<int> Numbers);

    [Fact]
    public void Equals_WithSameItemsInSameOrder_IsTrue()
    {
        ValueList<int> first = [1, 2, 3];
        ValueList<int> second = ValueList.From(new List<int> { 1, 2, 3 });

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentOrderOrItems_IsFalse()
    {
        ValueList<int> list = [1, 2, 3];

        Assert.NotEqual(list, [3, 2, 1]);
        Assert.NotEqual(list, [1, 2]);
    }

    [Fact]
    public void RecordsHoldingValueLists_CompareByContent()
    {
        Assert.Equal(new Holder("a", [1, 2]), new Holder("a", [1, 2]));
    }

    [Fact]
    public void From_CopiesSource_SoLaterChangesAreIgnored()
    {
        List<int> source = [1];

        ValueList<int> list = ValueList.From(source);
        source.Add(2);

        Assert.Single(list);
    }

    [Fact]
    public void Empty_CollectionExpression_HasNoItems()
    {
        ValueList<string> list = [];

        Assert.Empty(list);
        Assert.Equal(ValueList.From(Array.Empty<string>()), list);
    }
}
