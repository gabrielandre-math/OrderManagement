using Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Interceptors;

namespace Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>()
                );
            options.UseNpgsql(configuration.GetConnectionString("DefaultSchema"));
        });
        return services;
    }
}
