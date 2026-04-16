using Catalog.Data;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.DeleteProduct;

public class DeleteProductHandler(
    CatalogDbContext db,
    IStringLocalizer<CatalogMessages> localizer)
    : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(
        DeleteProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([command.Id], cancellationToken);

        if (product is null)
            return Result.Failure(
                Error.NotFound("Product-Not-Found",
                localizer["ProductNotFound", command.Id]));

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
