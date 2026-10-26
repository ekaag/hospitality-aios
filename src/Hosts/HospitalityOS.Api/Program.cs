using HospitalityOS.Api.Persistence;
using HospitalityOS.Api.Tenancy;
using HospitalityOS.Bindings;
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
builder.Services.AddConfiguredPersistence(builder.Configuration);
builder.Services.AddScoped<ICurrentTenant, UnresolvedCurrentTenant>();
builder.Services.AddHealthChecks().AddBindingHealthChecks();

WebApplication app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Entry point marker so integration tests can host the API via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
