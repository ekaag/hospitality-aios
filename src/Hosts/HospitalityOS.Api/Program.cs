using HospitalityOS.Adapters.Local;
using HospitalityOS.Api.Capabilities;
using HospitalityOS.Api.Execution;
using HospitalityOS.Api.Persistence;
using HospitalityOS.Application;
using HospitalityOS.Bindings;
using HospitalityOS.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Eventing;
using HospitalityOS.IntegrationRuntime;
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
// The tenant is set per scope: from claims once authentication arrives (F1), from the envelope when handling events.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ICurrentTenant>(services => services.GetRequiredService<TenantContext>());
builder.Services.AddEventing(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddCapabilities().AddBuiltInCapabilities();
// Providers come from integration projects (E3); each property's configuration selects one per connector.
builder.Services.AddIntegrationRuntime();
builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
builder.Services.AddHealthChecks().AddBindingHealthChecks();

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.MapHealthChecks("/health");
app.MapCapabilityEndpoints();

app.Run();

/// <summary>
/// Entry point marker so integration tests can host the API via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
