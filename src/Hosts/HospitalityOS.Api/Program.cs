using HospitalityOS.Adapters.Local;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Capabilities;
using HospitalityOS.Api.Execution;
using HospitalityOS.Api.Organizations;
using HospitalityOS.Api.Persistence;
using HospitalityOS.Api.Properties;
using HospitalityOS.Application;
using HospitalityOS.Bindings;
using HospitalityOS.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Eventing;
using HospitalityOS.IntegrationRuntime;
using HospitalityOS.Integrations.Mock;
using HospitalityOS.Integrations.Native;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// P15: wiring errors (missing registrations, singletons capturing scoped services) fail at startup in
// every environment, not only Development.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddBindings(builder.Configuration);
builder.Services.AddLocalAdapters();
builder.Services.AddPortBinding<IIdGenerator>();
builder.Services.AddPortBinding<IEventTransport>();
builder.Services.AddConfiguredPersistence(builder.Configuration);
// The tenant is set per scope: from the caller's token for requests, from the envelope when handling events.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ICurrentTenant>(services => services.GetRequiredService<TenantContext>());
builder.Services.AddEventing(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddCapabilities().AddBuiltInCapabilities();
// Providers available to every property; each property's integration configuration selects one per connector.
builder.Services.AddIntegrationRuntime();
builder.Services.AddMockIntegrations();
builder.Services.AddNativeIntegrations();
builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
builder.Services.AddHealthChecks().AddBindingHealthChecks();
builder.Services.AddStaffAuthentication();

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<CallerContextMiddleware>();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapCapabilityEndpoints();
app.MapOrganizationEndpoints();
app.MapPropertyEndpoints();

app.Run();

/// <summary>
/// Entry point marker so integration tests can host the API via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
