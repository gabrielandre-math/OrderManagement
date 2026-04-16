using Microsoft.EntityFrameworkCore;

namespace Basket.Data;

public class BasketDbContext : DbContext
{
    public BasketDbContext(DbContextOptions<BasketDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("basket");
        builder.ApplyConfigurationsFromAssembly(typeof(BasketDbContext).Assembly);
    }
}
