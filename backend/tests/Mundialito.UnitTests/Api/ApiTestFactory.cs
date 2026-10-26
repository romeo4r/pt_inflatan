using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Idempotency;
using Mundialito.Application.Abstractions.Messaging;
using NSubstitute;

namespace Mundialito.UnitTests.Api;

/// <summary>
/// Boots the real HTTP pipeline (routing, binding, middleware, ProblemDetails) in memory with the
/// application layer replaced by a substitute <see cref="ISender"/> and the idempotency store by
/// an in-memory one. No database is touched: the connection string is a placeholder that is never
/// opened. This isolates "does the API translate HTTP correctly" from "does the business logic
/// work", which the handler tests already cover.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public ISender Sender { get; } = Substitute.For<ISender>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", "Server=localhost;Database=Placeholder;User Id=x;Password=x;TrustServerCertificate=True");
        // No database behind the placeholder: startup must not try to migrate or seed.
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Sender);
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        });
    }
}
