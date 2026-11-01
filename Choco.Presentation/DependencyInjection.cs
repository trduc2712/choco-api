using System.Security.Claims;

using Choco.Application.Abstractions.Authentication;
using Choco.Presentation.Authentication;
using Choco.Presentation.OpenApi;
using Choco.Presentation.Options;
using Choco.Presentation.RateLimiting;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

using Svix;

namespace Choco.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClerkOptions>()
            .Bind(configuration.GetSection(ClerkOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddApiDocumentation(configuration);
        services.AddControllers();
        services.AddHealthChecks();
        services.AddProblemDetails();
        services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddUserRateLimiting(configuration);

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ClerkOptions>>((jwtBearerOptions, clerkOptions) =>
            {
                jwtBearerOptions.Authority = clerkOptions.Value.Authority;
                jwtBearerOptions.MapInboundClaims = false;
                jwtBearerOptions.TokenValidationParameters.ValidateAudience = false;
                jwtBearerOptions.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(5);

                jwtBearerOptions.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        string? azp = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Azp);

                        if (azp is null)
                        {
                            context.Fail($"The '{JwtRegisteredClaimNames.Azp}' claim is missing.");

                            return Task.CompletedTask;
                        }

                        if (!clerkOptions.Value.AuthorizedParties.Contains(azp, StringComparer.OrdinalIgnoreCase))
                        {
                            context.Fail($"The '{JwtRegisteredClaimNames.Azp}' claim '{azp}' is not an authorized party.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        services.AddSingleton(serviceProvider =>
        {
            ClerkOptions clerkOptions = serviceProvider.GetRequiredService<IOptions<ClerkOptions>>().Value;

            return new Webhook(clerkOptions.WebhookSigningSecret);
        });

        return services;
    }
}
