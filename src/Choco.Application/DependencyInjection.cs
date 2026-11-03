using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Behaviors;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace Choco.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(AssemblyReference.Assembly);
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
