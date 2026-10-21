using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Messaging;

/// <summary>Base marker for anything that can be sent through the <see cref="ISender"/>.</summary>
public interface IRequest<TResponse>
    where TResponse : Result
{
}

/// <summary>Marker shared by every command, used by pipeline behaviors to tell writes from reads.</summary>
public interface IBaseCommand
{
}

/// <summary>Write operation without a return value (write side of CQRS, handled with EF Core).</summary>
public interface ICommand : IRequest<Result>, IBaseCommand
{
}

/// <summary>Write operation that returns a value, typically the id of the created resource.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand
{
}

/// <summary>Read operation (read side of CQRS, handled with Dapper). Never changes state.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
