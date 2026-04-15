using Catalog.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    // Create a table for the Product entity in the PostgreSQL database
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("catalog");
        builder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }

}
