using Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;
using Shared.Pagination;

namespace Catalog.Products.Features.GetProducts;

public class GetProductHandler(CatalogDbContext db)
    : IQueryHandler<GetProductQuery, Result<PaginatedResult<ProductDto>>>
{
    public async Task<Result<PaginatedResult<ProductDto>>> Handle(
        GetProductQuery query,
        CancellationToken cancellationToken)
    {
        var count = await db.Products.LongCountAsync(cancellationToken);

        var products = await db.Products
            .OrderBy(p => p.Name)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.ImageUrl,
                p.Price))
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<ProductDto>(query.PageIndex, query.PageSize, count, products);

        return Result.Success(result);
    }
}
