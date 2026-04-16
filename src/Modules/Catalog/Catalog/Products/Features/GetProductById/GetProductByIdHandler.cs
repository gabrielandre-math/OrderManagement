using Catalog.Data;
using Catalog.Products.Features.GetProducts;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.GetProductById;

public class GetProductByIdHandler(CatalogDbContext db, IStringLocalizer<CatalogMessages> localizer)
    : IQueryHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([query.Id], cancellationToken);
        if (product is null)
            return Result.Failure<ProductDto>(
                Error.NotFound("Product-Not-Found",
                localizer["ProductNotFound", query.Id]));

        var dto = new ProductDto(product.Id, product.Name, product.Description, product.ImageUrl, product.Price);

        return Result.Success(dto);
    }
}
