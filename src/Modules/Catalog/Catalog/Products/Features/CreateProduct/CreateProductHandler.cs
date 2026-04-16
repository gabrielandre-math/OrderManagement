using Catalog.Data;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.CreateProduct;

public class CreateProductHandler(CatalogDbContext db)
    : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProductCommand command, 
        CancellationToken cancellationToken)
    {

        // Maybe a Factory Method inside the Product Entity
        // would be better to encapsulate the creation logic and ensure that
        // the entity is always in a valid state when created.
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            ImageUrl = command.ImageUrl,
            Price = command.Price,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(product.Id);
    }
}
