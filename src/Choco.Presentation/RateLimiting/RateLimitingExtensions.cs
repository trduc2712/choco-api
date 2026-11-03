using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Choco.Presentation.RateLimiting;

internal static class RateLimitingExtensions
{
    private const string ExceededErrorCode = "RateLimit.Exceeded";

    public static IServiceCollection AddUserRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((options, rateLimiting) =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    string? identityId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

                    if (identityId is null)
                    {
                        return RateLimitPartition.GetNoLimiter(string.Empty);
                    }

                    return RateLimitPartition.GetTokenBucketLimiter(identityId, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = rateLimiting.Value.TokenLimit,
                        TokensPerPeriod = rateLimiting.Value.TokensPerPeriod,
                        ReplenishmentPeriod = rateLimiting.Value.ReplenishmentPeriod,
                        QueueLimit = 0
                    });
                });
                options.OnRejected = WriteRejectionAsync;
            });

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        IProblemDetailsService problemDetailsService =
            context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Too many requests. Try again after the number of seconds in the Retry-After header.",
                Extensions = { ["code"] = ExceededErrorCode }
            }
        });
    }
}
