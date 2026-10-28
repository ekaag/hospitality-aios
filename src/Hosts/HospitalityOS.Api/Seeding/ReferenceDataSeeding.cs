using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.Seeding;
using HospitalityOS.SharedKernel.Execution;

namespace HospitalityOS.Api.Seeding;

/// <summary>
/// Seeds the Aurora Grand while the host starts, before it serves requests, in the Aurora Grand's tenant and acting as
/// the system. A failure stops the host: a half-seeded reference property would mislead every demo and test run on it.
/// </summary>
internal sealed partial class ReferenceDataSeeding(IServiceScopeFactory scopes, ILogger<ReferenceDataSeeding> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(AuroraGrand.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(new Actor(ActorType.System, "reference-data-seed"));

        SeedReport report = await scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>().SeedAuroraGrandAsync(cancellationToken).ConfigureAwait(false);

        LogSeeded(AuroraGrand.TenantId.Value, report.Created, report.Existing);

        foreach (SkippedStay skipped in report.StayImport.Skipped)
        {
            LogStaySkipped(skipped.PmsStayId.Value, skipped.Reason);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Aurora Grand seeded in tenant {TenantId}: {Created} records created, {Existing} already present")]
    private partial void LogSeeded(Guid tenantId, int created, int existing);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The seed's PMS stay import skipped {PmsStayId}: {Reason}")]
    private partial void LogStaySkipped(string pmsStayId, string reason);
}
