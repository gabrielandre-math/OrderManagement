using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.CreateProduct;

public record CreateProductRequest(
    string Name, 
    string? Description,
    string? ImageUrl, 
    decimal Price);

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (
            CreateProductRequest request, ISender sender) =>
        {
            var command = new CreateProductCommand(
                request.Name, 
                request.Description, 
                request.ImageUrl, 
                request.Price);

            var result = await sender.Send(command);

            return result.ToCreatedResult($"/api/products/{{result.Value}}");
        }).WithName("CreateProduct")
        .WithSummary("Cria um novo produto")
        .WithDescription("Adiciona um novo produto ao catálogo e retorna o ID gerado.")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);;
    }
}
