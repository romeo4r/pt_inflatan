using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;
using Mundialito.UnitTests.Application.Messaging;

namespace Mundialito.UnitTests.Application.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithValidRequest_ShouldReachHandler()
    {
        var sender = BuildSender();

        var result = await sender.Send(new CreateThingCommand("ok", 5));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("created:ok");
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldReturnValidationErrorForGenericResult()
    {
        var sender = BuildSender();

        var result = await sender.Send(new CreateThingCommand("", 0));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        var validationError = result.Error.ShouldBeOfType<ValidationError>();
        validationError.Code.ShouldBe(ValidationError.DefaultCode);
        validationError.Errors.Keys.ShouldBe(["name", "quantity"], ignoreOrder: true);
        validationError.Errors["name"].ShouldContain("El nombre es obligatorio.");
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldReturnValidationErrorForNonGenericResult()
    {
        var sender = BuildSender();

        var result = await sender.Send(new DeleteThingCommand(Guid.Empty));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>().Errors.Keys.ShouldBe(["id"]);
    }

    [Fact]
    public async Task Handle_WithoutValidators_ShouldReachHandler()
    {
        var sender = BuildSender();

        var result = await sender.Send(new NoRulesCommand());

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldNotInvokeHandler()
    {
        var calls = new List<string>();
        var sender = BuildSender(calls);

        await sender.Send(new CreateThingCommand("", 0));

        calls.ShouldBeEmpty();
    }

    private static ISender BuildSender(List<string>? calls = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(calls ?? []);
        services.AddScoped(typeof(ISender), DispatcherAccessor.DispatcherType);
        services.AddScoped(typeof(IPipelineBehavior<,>), DispatcherAccessor.ValidationBehaviorType);
        services.AddScoped<IValidator<CreateThingCommand>, CreateThingValidator>();
        services.AddScoped<IValidator<DeleteThingCommand>, DeleteThingValidator>();
        services.AddScoped<IRequestHandler<CreateThingCommand, Result<string>>, CreateThingHandler>();
        services.AddScoped<IRequestHandler<DeleteThingCommand, Result>, DeleteThingHandler>();
        services.AddScoped<IRequestHandler<NoRulesCommand, Result>, NoRulesHandler>();

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    public sealed record CreateThingCommand(string Name, int Quantity) : ICommand<string>;

    public sealed record DeleteThingCommand(Guid Id) : ICommand;

    public sealed record NoRulesCommand : ICommand;

    private sealed class CreateThingValidator : AbstractValidator<CreateThingCommand>
    {
        public CreateThingValidator()
        {
            RuleFor(c => c.Name).NotEmpty().WithMessage("El nombre es obligatorio.");
            RuleFor(c => c.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero.");
        }
    }

    private sealed class DeleteThingValidator : AbstractValidator<DeleteThingCommand>
    {
        public DeleteThingValidator()
        {
            RuleFor(c => c.Id).NotEmpty().WithMessage("El identificador es obligatorio.");
        }
    }

    private sealed class CreateThingHandler(List<string> calls) : ICommandHandler<CreateThingCommand, string>
    {
        public Task<Result<string>> Handle(CreateThingCommand request, CancellationToken cancellationToken)
        {
            calls.Add(nameof(CreateThingHandler));
            return Task.FromResult(Result.Success($"created:{request.Name}"));
        }
    }

    private sealed class DeleteThingHandler : ICommandHandler<DeleteThingCommand>
    {
        public Task<Result> Handle(DeleteThingCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class NoRulesHandler : ICommandHandler<NoRulesCommand>
    {
        public Task<Result> Handle(NoRulesCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }
}
