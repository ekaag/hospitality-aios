using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// Model conventions a database provider needs, e.g. SQLite storing <see cref="DateTimeOffset"/> as a number so it
/// can be sorted. Registered by the provider project.
/// </summary>
internal interface IProviderModelConventions
{
    void Configure(ModelConfigurationBuilder configurationBuilder);
}
