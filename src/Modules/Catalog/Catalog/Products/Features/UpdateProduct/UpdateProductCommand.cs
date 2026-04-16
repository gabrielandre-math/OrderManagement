using Shared.Contracts.CQRS;

namespace Catalog.Products.Features.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    string? ImageUrl,
    decimal Price) : ICommand;
