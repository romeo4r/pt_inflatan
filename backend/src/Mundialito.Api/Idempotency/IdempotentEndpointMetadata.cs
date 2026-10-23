namespace Mundialito.Api.Idempotency;

/// <summary>Marks an endpoint as requiring the Idempotency-Key header. Read by <see cref="IdempotencyMiddleware"/>.</summary>
public sealed class IdempotentEndpointMetadata
{
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>Opts a (POST) endpoint into idempotent handling.</summary>
    public static TBuilder RequireIdempotencyKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new IdempotentEndpointMetadata());
        return builder;
    }
}
