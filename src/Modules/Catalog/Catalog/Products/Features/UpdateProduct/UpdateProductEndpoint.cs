using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.UpdateProduct;

public record UpdateProductRequest(string Name, string? Description, string? ImageUrl, decimal Price);

public class UpdateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/products/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(id, request.Name, request.Description, request.ImageUrl, request.Price);
            var result = await sender.Send(command);
            return result.ToProblemResult();
        });
    }
}
