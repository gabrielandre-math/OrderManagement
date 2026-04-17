using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        }).WithName("UpdateProduct")
        .WithSummary("Atualiza um produto existente")
        .WithDescription("Altera as informações de um produto. Se o ID não for encontrado, retorna 404.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);;
    }
}
