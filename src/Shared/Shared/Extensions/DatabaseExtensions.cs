using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Seed;

namespace Shared.Extensions;

public static class DatabaseExtensions
{
    public static async Task<WebApplication> UseMigrationAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetService<IDataSeeder>();
        if (seeder is not null)
            await seeder.SeedAllAsync();

        return app;
    }
}