using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;
using Shared.Pagination;

namespace Catalog.Products.Features.GetProducts;

public class GetProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", async (int pageIndex, int pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetProductQuery(pageIndex, pageSize));
            return result.ToProblemResult();
        }).WithName("GetProducts")
        .WithSummary("List products with pagination")
        .WithDescription("Returns a paginated list of products from the catalog.")
        .Produces<PaginatedResult<ProductDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);;
    }
}
