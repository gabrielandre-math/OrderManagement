using Shared.DDD;

namespace Catalog.Domain;

public class Product : Entity<Guid>
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; } 
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
}
