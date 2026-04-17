using Catalog.Products.Features.GetProducts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Shared.Contracts.Results;

namespace Catalog.Products.Extensions;

public static class ProductExtensions
{
    public static async Task<Result<Product>> GetOrNotFoundAsync(
        this DbSet<Product> products, 
        Guid id,
        IStringLocalizer localizer, 
        CancellationToken ct)
    {
        var product = await products.FindAsync([id], ct);
    
        return product is not null 
            ? Result.Success(product) 
            : Result.Failure<Product>(Error.NotFound("Product-Not-Found", localizer["ProductNotFound", id]));
    }
}