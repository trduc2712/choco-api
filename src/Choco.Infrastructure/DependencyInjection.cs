using System.Net.Http.Headers;

using Amazon.Runtime;
using Amazon.S3;

using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Storage;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.Infrastructure.Authentication;
using Choco.Infrastructure.Options;
using Choco.Infrastructure.Persistence;
using Choco.Infrastructure.Persistence.Interceptors;
using Choco.Infrastructure.Persistence.Repositories;
using Choco.Infrastructure.Persistence.Seeding;
using Choco.Infrastructure.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Choco.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<ChocoDbContext>((serviceProvider, options) =>
        {
            DatabaseOptions databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            AuditInterceptor auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
            SoftDeleteInterceptor softDeleteInterceptor = serviceProvider.GetRequiredService<SoftDeleteInterceptor>();

            options.UseNpgsql(databaseOptions.ConnectionString);
            options.UseSnakeCaseNamingConvention();
            options.AddInterceptors(softDeleteInterceptor, auditInterceptor);
            options.UseSeeding(SystemRoleSeeder.Seed);
            options.UseAsyncSeeding(SystemRoleSeeder.SeedAsync);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<ChocoDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserFollowRepository, UserFollowRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IPostLikeRepository, PostLikeRepository>();

        services.AddOptions<AdministratorBootstrapOptions>()
            .Bind(configuration.GetSection(AdministratorBootstrapOptions.SectionName));

        services.AddOptions<ClerkOptions>()
            .Bind(configuration.GetSection(ClerkOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IIdentityProvider, ClerkIdentityProvider>((serviceProvider, client) =>
        {
            ClerkOptions clerkOptions = serviceProvider.GetRequiredService<IOptions<ClerkOptions>>().Value;

            client.BaseAddress = clerkOptions.BackendApiUrl;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clerkOptions.SecretKey);
        });

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            StorageOptions storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;

            AmazonS3Config config = new()
            {
                ServiceURL = storageOptions.ServiceUrl,
                AuthenticationRegion = storageOptions.Region,
                ForcePathStyle = true,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };

            return new AmazonS3Client(
                new BasicAWSCredentials(storageOptions.AccessKeyId, storageOptions.SecretAccessKey), config);
        });

        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        return services;
    }
}
