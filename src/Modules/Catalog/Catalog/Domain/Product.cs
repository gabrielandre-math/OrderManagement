using Shared.Contracts.Results;
using Shared.DDD;

namespace Catalog.Products;

public class Product : Entity<Guid>
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; } 
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    
    private Product() { }

    public static Result<Product> Create(string name, string? description, string? imageUrl, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Product>(Error.Validation("Product.NameRequired"));
        if (price < 0)
            return Result.Failure<Product>(Error.Validation("Product.NegativePrice"));
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ImageUrl = imageUrl,
            Price = price
        };

        return Result.Success(product);
    }
    
    public void Update(string name, string? description, string? imageUrl, decimal price)
    {
        Name = name;
        Description = description;
        ImageUrl = imageUrl;
        Price = price;
    }
}
