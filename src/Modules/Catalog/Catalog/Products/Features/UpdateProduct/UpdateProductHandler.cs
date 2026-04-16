using Catalog.Data;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.UpdateProduct;

public class UpdateProductHandler(
    CatalogDbContext db,
    IStringLocalizer<CatalogMessages> localizer)
    : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([command.Id], cancellationToken);
        if (product is null)
            return Result.Failure(
                Error.NotFound("Product-Not-Found",
                localizer["ProductNotFound", command.Id]));

        // A Factory method can be used here to create a
        // new instance of the Product with the updated values,
        // instead of modifying the existing instance.
        product.Name = command.Name;
        product.Description = command.Description;
        product.ImageUrl = command.ImageUrl;
        product.Price = command.Price;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();

    }
}
