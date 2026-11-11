using System.Net;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Choco.Presentation.ForwardedHeaders;

internal static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddConfiguredForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersSettings>()
            .Bind(configuration.GetSection(ForwardedHeadersSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
            {
                ForwardedHeadersSettings value = settings.Value;

                if (!value.Enabled)
                {
                    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.None;

                    return;
                }

                options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                    | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = value.ForwardLimit;

                if (value.TrustAnyProxy)
                {
                    options.KnownProxies.Clear();
                    options.KnownIPNetworks.Clear();

                    return;
                }

                foreach (string proxy in value.KnownProxies)
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }

                foreach (string network in value.KnownNetworks)
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });

        return services;
    }
}
