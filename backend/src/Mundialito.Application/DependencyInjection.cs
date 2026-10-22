using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Behaviors;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dispatcher, every command/query handler and domain event handler found in
    /// this assembly, the pipeline behaviors (outermost first) and every FluentValidation validator.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISender, Dispatcher>();

        services.AddImplementationsOfOpenGeneric(typeof(IRequestHandler<,>));
        services.AddImplementationsOfOpenGeneric(typeof(IDomainEventHandler<>));

        // Order matters: logging wraps validation, so failed validations are logged too.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        return services;
    }

    private static void AddImplementationsOfOpenGeneric(this IServiceCollection services, Type openGenericInterface)
    {
        var registrations = AssemblyReference.Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface)
                .Select(service => (service, implementation: type)));

        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }
    }
}
