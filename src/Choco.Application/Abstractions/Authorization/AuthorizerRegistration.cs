using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Choco.Application.Abstractions.Authorization;

internal static class AuthorizerRegistration
{
    public static IServiceCollection AddAuthorizersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        IEnumerable<(Type Service, Type Implementation)> registrations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IAuthorizer<>))
                .Select(contract => (contract, type)));

        foreach ((Type service, Type implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}
