using Microsoft.EntityFrameworkCore;

namespace Orders.Data;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("order");
        builder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}
