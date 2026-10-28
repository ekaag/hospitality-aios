using HospitalityOS.Integrations.Mock.Pms;
using HospitalityOS.Seeding;

namespace HospitalityOS.Api.Seeding;

/// <summary>
/// Seeds the Aurora Grand reference property at startup when <c>Seed:AuroraGrand:Enabled</c> is true (the local launch
/// profile sets it; deployments may, as their seed step). Off by default, so hosts that do not ask for it, including
/// test hosts, start empty.
/// </summary>
internal static class SeedRegistration
{
    public const string EnabledKey = "Seed:AuroraGrand:Enabled";

    public static IServiceCollection AddConfiguredSeeding(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>(EnabledKey))
        {
            return services;
        }

        services.AddSeeding();

        // The mock PMS holds Alex's reservation, dated from the property's today, for the seed's stay import to link.
        services.AddOptions<MockPmsOptions>().Configure<TimeProvider>((options, timeProvider) =>
            options.Stays[AuroraGrand.PropertyId] = [.. AuroraGrand.PmsStays(AuroraGrand.LocalToday(timeProvider.GetUtcNow()))]);
        services.AddHostedService<ReferenceDataSeeding>();

        return services;
    }
}
