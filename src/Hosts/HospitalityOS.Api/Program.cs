using HospitalityOS.Bindings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddBindings(builder.Configuration);
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
