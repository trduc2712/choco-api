using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Authorization;
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
            config.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            config.AddOpenBehavior(typeof(AuditBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        services.AddAuthorizersFromAssembly(AssemblyReference.Assembly);
        services.AddAuditDescribersFromAssembly(AssemblyReference.Assembly);

        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<Users.MyProfileWriter>();
        services.AddScoped<Posts.PostResponseReader>();
        services.AddScoped<Posts.VisiblePostFinder>();
        services.AddScoped<Notifications.NotificationDispatcher>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();

        return services;
    }
}
