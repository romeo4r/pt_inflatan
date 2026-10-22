using Mundialito.Application;
using Mundialito.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException(
        "Connection string 'Database' is not configured. Set ConnectionStrings:Database via user secrets or environment variables.");

builder.Services
    .AddApplication()
    .AddInfrastructure(connectionString);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness endpoint used by Docker Compose health checks and smoke tests.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .WithName("Health");

app.Run();
