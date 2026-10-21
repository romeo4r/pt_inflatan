var builder = WebApplication.CreateBuilder(args);

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
