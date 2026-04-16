using Shared.Contracts.CQRS;
using Shared.Contracts.Results;
using Shared.Pagination;

namespace Catalog.Products.Features.GetProducts;

public record GetProductQuery(
    int PageIndex = 0, 
    int PageSize = 10) 
    : IQuery<Result<PaginatedResult<ProductDto>>>;

public record ProductDto(
    Guid Id, 
    string Name, 
    string? Description, 
    string? ImageUrl, 
    decimal Price);