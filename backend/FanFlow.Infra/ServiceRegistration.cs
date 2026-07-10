using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration);

        services.AddScoped<IIdGenerator, GuidIdGenerator>();
        services.AddScoped<IClock, SystemClock>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();

        services.AddScoped<IReleaseRepository>(_ =>
            new PostgresReleaseRepository(configuration.GetConnectionString("DefaultConnection")!));
        services.AddScoped<IEventRepository>(_ =>
            new PostgresEventRepository(configuration.GetConnectionString("DefaultConnection")!));

        services.AddScoped<ISlugGenerator, SlugGenerator>();

        var minioBucket = configuration["Publishing:Minio:Bucket"] ?? "releases";
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            configuration["Publishing:Minio:AccessKey"],
            configuration["Publishing:Minio:SecretKey"],
            new AmazonS3Config
            {
                ServiceURL = configuration["Publishing:Minio:Endpoint"],
                ForcePathStyle = true
            }));
        services.AddScoped<IReleasePublisher>(sp =>
            new StaticSiteReleasePublisher(sp.GetRequiredService<IAmazonS3>(), minioBucket));

        var publicHostname = configuration["Publishing:PublicHostname"];
        services.AddScoped<IPublicSiteSettings>(_ => new PublicSiteSettings(publicHostname));

        // Null Object pattern: Meta requires real credentials that aren't available in every
        // environment (dev/test), so the flag is read once here at the composition root.
        var metaConversionsEnabled = configuration.GetValue<bool>("Features:MetaConversions:Enabled");
        if (metaConversionsEnabled)
        {
            services.AddHttpClient();
            services.AddScoped<IConversionsApiClient>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MetaConversionsApiClient));
                return new MetaConversionsApiClient(
                    httpClient,
                    configuration["Meta:AccessToken"] ?? "");
            });
        }
        else
        {
            services.AddScoped<IConversionsApiClient, NullConversionsApiClient>();
        }

        return services;
    }

    /// <summary>
    /// Ensures the MinIO publishing bucket exists and is publicly readable. Called once from
    /// Program.cs at startup, alongside the database migration runner.
    /// </summary>
    public static async Task EnsurePublishingBucketAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var s3Client = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var bucket = configuration["Publishing:Minio:Bucket"] ?? "releases";

        await StaticSiteReleasePublisher.EnsureBucketAsync(s3Client, bucket);
    }
}
