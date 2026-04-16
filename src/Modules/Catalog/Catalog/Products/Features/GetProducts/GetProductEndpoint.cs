using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.GetProducts;

public class GetProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", async (int pageIndex, int pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetProductQuery(pageIndex, pageSize));
            return result.ToProblemResult();
        });
    }
}
