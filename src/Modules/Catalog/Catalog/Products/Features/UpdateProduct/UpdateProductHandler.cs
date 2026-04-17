using Catalog.Data;
using Catalog.Products.Extensions;
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
    public async Task<Result> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var result = await db.Products.GetOrNotFoundAsync(command.Id, localizer, cancellationToken);

        result.Tap(p => p.Update(command.Name, command.Description, command.ImageUrl, command.Price));

        if (result.IsFailure) return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
