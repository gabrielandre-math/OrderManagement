using Catalog.Data;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.CreateProduct;

public class CreateProductHandler(CatalogDbContext db, IStringLocalizer<CatalogMessages> localizer)
    : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var result = Product.Create(command.Name, command.Description, command.ImageUrl, command.Price);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error.ToLocalized(localizer));
        
        var product = result.Value;
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(product.Id);
    }
}
