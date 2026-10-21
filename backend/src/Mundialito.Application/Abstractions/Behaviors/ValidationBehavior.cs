using System.Reflection;
using FluentValidation;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Behaviors;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> registered for the request before the handler executes.
/// On failure it short-circuits with a <see cref="ValidationError"/> wrapped in the expected
/// <see cref="Result"/> type, so the handler never sees invalid input and no exception is thrown.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private static readonly Func<ValidationError, TResponse> FailureFactory = BuildFailureFactory();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length == 0)
        {
            return await next();
        }

        var errors = failures
            .GroupBy(f => ToCamelCase(f.PropertyName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray(), StringComparer.Ordinal);

        return FailureFactory(new ValidationError(errors));
    }

    /// <summary>
    /// Builds, once per closed generic type, a delegate that produces a failed TResponse,
    /// whether TResponse is <see cref="Result"/> or <see cref="Result{T}"/>.
    /// </summary>
    private static Func<ValidationError, TResponse> BuildFailureFactory()
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return error => (TResponse)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition)
            .MakeGenericMethod(valueType);

        return error => (TResponse)failureMethod.Invoke(null, [error])!;
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
        {
            return propertyName;
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }
}
