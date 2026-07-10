using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Infra;

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

        var publishingDirectory = configuration["Publishing:OutputDirectory"] ?? "./publish";
        services.AddScoped<IReleasePublisher>(_ => new StaticSiteReleasePublisher(publishingDirectory));

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
                    configuration["Meta:PixelId"] ?? "",
                    configuration["Meta:AccessToken"] ?? "");
            });
        }
        else
        {
            services.AddScoped<IConversionsApiClient, NullConversionsApiClient>();
        }

        return services;
    }
}
