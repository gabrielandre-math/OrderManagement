using Catalog.Data;
using Catalog.Products.Extensions;
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
    public async Task<Result> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var result = await db.Products.GetOrNotFoundAsync(command.Id, localizer, cancellationToken);

        if (result.IsFailure) return result;
        
        db.Products.Remove(result.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}