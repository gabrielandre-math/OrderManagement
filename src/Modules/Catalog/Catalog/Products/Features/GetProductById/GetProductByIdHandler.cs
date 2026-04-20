using Catalog.Data;
using Catalog.Products.Extensions;
using Catalog.Products.Features.GetProducts;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.GetProductById;

public class GetProductByIdHandler(
    CatalogDbContext db, 
    IStringLocalizer<CatalogMessages> localizer) 
    : IQueryHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery query, 
        CancellationToken cancellationToken)
    {
        return (await db.Products.GetOrNotFoundAsync(query.Id, localizer, cancellationToken))
            .Map(p => new ProductDto(p.Id, p.Name, p.Description, p.ImageUrl, p.Price));
    }
}