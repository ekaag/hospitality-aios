using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.SharedKernel.Tenancy;

/// <summary>
/// Marks an aggregate as belonging to one property, so its events can name the property in their envelope
/// (spec §8) even when the event itself carries only the aggregate's ID.
/// </summary>
public interface IPropertyScoped
{
    /// <summary>
    /// The property the aggregate belongs to.
    /// </summary>
    PropertyId PropertyId { get; }
}
