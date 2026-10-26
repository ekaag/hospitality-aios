using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;

namespace HospitalityOS.UnitTests.Eventing.TestDoubles;

/// <summary>
/// Hands out the same ID every time, bound as the default <see cref="IIdGenerator"/>.
/// </summary>
internal sealed class FixedIdGenerator(Guid id) : IIdGenerator, IBindingResolver<IIdGenerator>
{
    public Guid NewId() => id;

    public IIdGenerator Resolve(BindingContext context) => this;
}
