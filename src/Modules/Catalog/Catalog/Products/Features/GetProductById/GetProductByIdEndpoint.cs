using Carter;
using Catalog.Products.Features.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;
using Shared.Pagination;

namespace Catalog.Products.Features.GetProductById;

public class GetProductByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProductByIdQuery(id));
            return result.ToProblemResult();
        }).WithName("GetProductsById")
            .WithSummary("List by id products")
            .WithDescription("Returns a item by id  products from the catalog.")
            .Produces<PaginatedResult<ProductDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);;
    }
}