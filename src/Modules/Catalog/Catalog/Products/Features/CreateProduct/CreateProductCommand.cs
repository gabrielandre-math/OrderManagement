using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.CreateProduct;

public record CreateProductCommand(
    string Name, 
    string? Description,
    string? ImageUrl, 
    decimal Price) : ICommand<Result<Guid>>;