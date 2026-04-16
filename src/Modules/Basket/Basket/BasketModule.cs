using Basket.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Interceptors;

namespace Basket;

public static class BasketModule
{
    public static IServiceCollection AddBasketModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BasketDbContext>((sp, options) =>
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
