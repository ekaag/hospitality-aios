using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalityOS.Seeding;

/// <summary>
/// Registers the reference data seeder. The host must register the application services (<c>AddApplication</c>), the
/// integration runtime with the mock PMS, native CMS, and mock messaging providers, and persistence; and give the mock
/// PMS <see cref="AuroraGrand.PmsStays"/> for <see cref="AuroraGrand.PropertyId"/>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IReferenceDataSeeder"/> (scoped).
    /// </summary>
    public static IServiceCollection AddSeeding(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<SeedSteps>();
        services.TryAddScoped<AuroraGrandProperty>();
        services.TryAddScoped<AuroraGrandOfferings>();
        services.TryAddScoped<AuroraGrandGuests>();
        services.TryAddScoped<IReferenceDataSeeder, AuroraGrandSeeder>();

        return services;
    }
}
