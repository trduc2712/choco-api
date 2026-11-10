using System.Security.Claims;

using Asp.Versioning;

using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Notifications;
using Choco.Presentation.Authentication;
using Choco.Presentation.Authorization;
using Choco.Presentation.Notifications;
using Choco.Presentation.OpenApi;
using Choco.Presentation.Options;
using Choco.Presentation.RateLimiting;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.SignalR;
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

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()));

        services.AddControllers();
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        })
        .AddApiDocumentation(configuration);
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

        services.AddHostedService<AdministratorBootstrapHostedService>();

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
                    OnMessageReceived = context =>
                    {
                        string? accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments(NotificationsHub.Path))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
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

        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, IdentityUserIdProvider>();
        services.AddSingleton<INotificationRealtimePublisher, NotificationRealtimePublisher>();

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
