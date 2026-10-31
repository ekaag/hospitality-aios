using HospitalityOS.Adapters.Anthropic;
using HospitalityOS.Adapters.Local;
using HospitalityOS.Ai;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Api.Ai;
using HospitalityOS.Api.Amenities;
using HospitalityOS.Api.Authentication;
using HospitalityOS.Api.Capabilities;
using HospitalityOS.Api.Content;
using HospitalityOS.Api.Conversations;
using HospitalityOS.Api.Events;
using HospitalityOS.Api.Execution;
using HospitalityOS.Api.Hours;
using HospitalityOS.Api.Http;
using HospitalityOS.Api.Menus;
using HospitalityOS.Api.Organizations;
using HospitalityOS.Api.Persistence;
using HospitalityOS.Api.Policies;
using HospitalityOS.Api.Properties;
using HospitalityOS.Api.Restaurants;
using HospitalityOS.Api.Seeding;
using HospitalityOS.Api.Services;
using HospitalityOS.Api.Spaces;
using HospitalityOS.Api.Staff;
using HospitalityOS.Application;
using HospitalityOS.Bindings;
using HospitalityOS.Capabilities;
using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Eventing;
using HospitalityOS.IntegrationRuntime;
using HospitalityOS.Integrations.Mock;
using HospitalityOS.Integrations.Native;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Scheduling;
using HospitalityOS.Ports.Search;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.Workflows;
using HospitalityOS.Workflows.Routing;

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
builder.Services.AddAnthropicAdapters();
builder.Services.AddPortBinding<IIdGenerator>();
builder.Services.AddPortBinding<IEventTransport>();
builder.Services.AddPortBinding<IScheduler>();
builder.Services.AddPortBinding<ISearchIndex>();
builder.Services.AddPortBinding<IVectorStore>();
// Inference tasks are declared by the modules that run them (from G4 on) and routed by the Inference section.
builder.Services.AddInference(builder.Configuration);
builder.Services.AddConfiguredPersistence(builder.Configuration);
// The tenant is set per scope: from the caller's token for requests, from the envelope when handling events.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ICurrentTenant>(services => services.GetRequiredService<TenantContext>());
builder.Services.AddEventing(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddCapabilities().AddBuiltInCapabilities();
builder.Services.Configure<ContentSearchOptions>(builder.Configuration.GetSection(ContentSearchOptions.SectionName));
builder.Services.AddAi().AddContentIndexing();
builder.Services.Configure<AITraceRedactionOptions>(builder.Configuration.GetSection(AITraceRedactionOptions.SectionName));
// The autonomous controllers: the concierge answers guest messages (Inference:Tasks:concierge.answer picks its model).
builder.Services.AddWorkflows();
builder.Services.Configure<HumanActionRoutingOptions>(builder.Configuration.GetSection(HumanActionRoutingOptions.SectionName));
// Providers available to every property; each property's integration configuration selects one per connector.
builder.Services.AddIntegrationRuntime();
builder.Services.AddMockIntegrations();
builder.Services.AddNativeIntegrations();
// The Aurora Grand reference property, when Seed:AuroraGrand:Enabled (the local launch profile sets it).
builder.Services.AddConfiguredSeeding(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
// Requests that cannot be read are thrown to the exception handler, which answers with a coded problem.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<MalformedRequestHandler>();
builder.Services.AddHealthChecks().AddBindingHealthChecks().AddInferenceHealthChecks();
builder.Services.AddCallerAuthentication();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<CallerContextMiddleware>();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapCapabilityEndpoints();
app.MapOrganizationEndpoints();
app.MapPropertyEndpoints();
app.MapSpaceEndpoints();
app.MapAmenityEndpoints();
app.MapOperatingHoursEndpoints();
app.MapPolicyEndpoints();
app.MapContentEndpoints();
app.MapRestaurantEndpoints();
app.MapMenuEndpoints();
app.MapPropertyEventEndpoints();
app.MapServiceEndpoints();
app.MapStaffEndpoints();
app.MapAITraceEndpoints();
app.MapConversationEndpoints();

app.Run();

/// <summary>
/// Entry point marker so integration tests can host the API via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
