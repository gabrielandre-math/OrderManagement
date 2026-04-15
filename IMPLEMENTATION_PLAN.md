# 🎯 OrderManagementApi — Plano de Implementação para Entrevista

> **Ponto de partida:** Core commitado na tag `core-ready`.
> Resete com `git checkout core-ready` para praticar do zero.

---

## 📦 O que já está pronto (Core)

```
✅ Result Pattern          → Error, Result, Result<T>, ResultExtensions
✅ CQRS Abstractions       → ICommand, IQuery, ICommandHandler, IQueryHandler
✅ MediatR Behaviors       → ValidationBehavior, LoggingBehavior
✅ DDD Building Blocks     → Entity<T>, Aggregate<T>, IDomainEvent
✅ EF Core Interceptors    → AuditableEntityInterceptor (IHttpContextAccessor), DispatchDomainEventsInterceptor
✅ i18n                    → 3 locales (en-US, pt-BR, es), .resx por módulo
✅ Carter/MediatR helpers  → AddCarterWithAssemblies, AddMediatRWithAssemblies
✅ Exception Handler       → CustomExceptionHandler (safety net)
✅ Pagination              → PaginatedResult<T>
✅ Data Seeder             → IDataSeeder interface
✅ Messaging project       → Shared.Messaging com MassTransit.RabbitMQ
```

---

## 🗺️ Visão geral das fases

```
Fase 1 → Catalog Module (CRUD completo)             ~25 min
Fase 2 → Program.cs + Infra (PostgreSQL, DI)        ~10 min
Fase 3 → Basket Module (Redis cache)                ~15 min
Fase 4 → Orders Module (DDD + Domain Events)        ~15 min
Fase 5 → Messaging (RabbitMQ via MassTransit)        ~10 min
Fase 6 → Testes                                      ~10 min
                                                  TOTAL ~85 min
```

---

## FASE 1 — Módulo Catalog (CRUD completo)

> **Objetivo:** Mostrar domínio de Vertical Slice + CQRS + Result Pattern + i18n
> **Tempo estimado:** ~25 min

### 1.1 Entity — `Product`

📁 `src/Modules/Catalog/Catalog/Products/Models/Product.cs`

```csharp
using Shared.DDD;

namespace Catalog.Products.Models;

public class Product : Entity<Guid>
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal Price { get; set; }
    public List<string> Categories { get; set; } = [];

    public static Product Create(Guid id, string name, string description, decimal price, List<string> categories)
    {
        var product = new Product
        {
            Id = id,
            Name = name,
            Description = description,
            Price = price,
            Categories = categories
        };
        return product;
    }
}
```

> ⚠️ **Por que não é Aggregate?** Product é simples, sem domain events.
> Na entrevista, justifique: "Aggregate só quando há invariantes complexas ou eventos de domínio."

### 1.2 DbContext — `CatalogDbContext`

📁 `src/Modules/Catalog/Catalog/Data/CatalogDbContext.cs`

```csharp
using Catalog.Products.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("catalog");
        builder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
```

### 1.3 EF Configuration

📁 `src/Modules/Catalog/Catalog/Data/Configurations/ProductConfiguration.cs`

```csharp
using Catalog.Products.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Categories)
            .HasColumnType("text[]"); // PostgreSQL array nativo
    }
}
```

### 1.4 Data Seeder

📁 `src/Modules/Catalog/Catalog/Data/Seed/CatalogDataSeeder.cs`

```csharp
using Catalog.Products.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;

namespace Catalog.Data.Seed;

public class CatalogDataSeeder(CatalogDbContext context) : IDataSeeder
{
    public async Task SeedAllAsync()
    {
        if (!await context.Products.AnyAsync())
        {
            var products = new List<Product>
            {
                Product.Create(Guid.NewGuid(), "Wireless Mouse", "Ergonomic wireless mouse", 29.99m, ["Electronics", "Accessories"]),
                Product.Create(Guid.NewGuid(), "Mechanical Keyboard", "RGB mechanical keyboard", 79.99m, ["Electronics", "Accessories"]),
                Product.Create(Guid.NewGuid(), "USB-C Hub", "7-in-1 USB-C hub", 49.99m, ["Electronics", "Accessories"])
            };
            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }
}
```

### 1.5 Feature: CreateProduct

📁 `src/Modules/Catalog/Catalog/Products/Features/CreateProduct/CreateProductHandler.cs`

```csharp
using Catalog.Data;
using Catalog.Products.Models;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.CreateProduct;

public record CreateProductCommand(string Name, string Description, decimal Price, List<string> Categories)
    : ICommand<Result<CreateProductResult>>;

public record CreateProductResult(Guid Id);

internal class CreateProductHandler(CatalogDbContext context)
    : ICommandHandler<CreateProductCommand, Result<CreateProductResult>>
{
    public async Task<Result<CreateProductResult>> Handle(
        CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = Product.Create(
            Guid.NewGuid(), command.Name, command.Description, command.Price, command.Categories);

        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateProductResult(product.Id));
    }
}
```

📁 `src/Modules/Catalog/Catalog/Products/Features/CreateProduct/CreateProductValidator.cs`

```csharp
using FluentValidation;
using Microsoft.Extensions.Localization;
using Catalog.Resources;

namespace Catalog.Products.Features.CreateProduct;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator(IStringLocalizer<CatalogMessages> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer["ProductNameRequired"]);

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage(localizer["ProductPriceMustBePositive"]);
    }
}
```

📁 `src/Modules/Catalog/Catalog/Products/Features/CreateProduct/CreateProductEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Shared.Extensions;

namespace Catalog.Products.Features.CreateProduct;

public record CreateProductRequest(string Name, string Description, decimal Price, List<string> Categories);

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (CreateProductRequest request, ISender sender) =>
        {
            var command = new CreateProductCommand(
                request.Name, request.Description, request.Price, request.Categories);

            var result = await sender.Send(command);

            return result.ToCreatedResult($"/api/products/{result.Value?.Id}");
        })
        .WithName("CreateProduct")
        .Produces(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Products");
    }
}
```

### 1.6 Feature: GetProducts (com paginação)

📁 `src/Modules/Catalog/Catalog/Products/Features/GetProducts/GetProductsHandler.cs`

```csharp
using Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.CQRS;
using Shared.Pagination;

namespace Catalog.Products.Features.GetProducts;

public record GetProductsQuery(int PageIndex = 0, int PageSize = 10)
    : IQuery<GetProductsResult>;

public record ProductDto(Guid Id, string Name, string Description, decimal Price, List<string> Categories);

public record GetProductsResult(PaginatedResult<ProductDto> Products);

internal class GetProductsHandler(CatalogDbContext context)
    : IQueryHandler<GetProductsQuery, GetProductsResult>
{
    public async Task<GetProductsResult> Handle(
        GetProductsQuery query, CancellationToken cancellationToken)
    {
        var count = await context.Products.LongCountAsync(cancellationToken);

        var products = await context.Products
            .OrderBy(p => p.Name)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Categories))
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<ProductDto>(
            query.PageIndex, query.PageSize, count, products);

        return new GetProductsResult(result);
    }
}
```

📁 `src/Modules/Catalog/Catalog/Products/Features/GetProducts/GetProductsEndpoint.cs`

```csharp
using Carter;
using MediatR;

namespace Catalog.Products.Features.GetProducts;

public class GetProductsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", async (int? pageIndex, int? pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetProductsQuery(pageIndex ?? 0, pageSize ?? 10));
            return Results.Ok(result);
        })
        .WithName("GetProducts")
        .Produces<GetProductsResult>()
        .WithTags("Products");
    }
}
```

### 1.7 Feature: GetProductById

📁 `src/Modules/Catalog/Catalog/Products/Features/GetProductById/GetProductByIdHandler.cs`

```csharp
using Catalog.Data;
using Catalog.Products.Features.GetProducts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Catalog.Resources;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.GetProductById;

public record GetProductByIdQuery(Guid Id) : IQuery<Result<ProductDto>>;

internal class GetProductByIdHandler(CatalogDbContext context, IStringLocalizer<CatalogMessages> localizer)
    : IQueryHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .Where(p => p.Id == query.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Categories))
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            return Result.Failure<ProductDto>(
                Error.NotFound("Product.NotFound", localizer["ProductNotFound", query.Id]));

        return product;  // conversão implícita → Result.Success
    }
}
```

📁 `src/Modules/Catalog/Catalog/Products/Features/GetProductById/GetProductByIdEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Shared.Extensions;

namespace Catalog.Products.Features.GetProductById;

public class GetProductByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProductByIdQuery(id));
            return result.ToProblemResult();
        })
        .WithName("GetProductById")
        .Produces<GetProducts.ProductDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Products");
    }
}
```

### 1.8 Feature: UpdateProduct

📁 `src/Modules/Catalog/Catalog/Products/Features/UpdateProduct/UpdateProductHandler.cs`

```csharp
using Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Catalog.Resources;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.UpdateProduct;

public record UpdateProductCommand(Guid Id, string Name, string Description, decimal Price, List<string> Categories)
    : ICommand<Result>;

internal class UpdateProductHandler(CatalogDbContext context, IStringLocalizer<CatalogMessages> localizer)
    : ICommandHandler<UpdateProductCommand, Result>
{
    public async Task<Result> Handle(
        UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);

        if (product is null)
            return Result.Failure(
                Error.NotFound("Product.NotFound", localizer["ProductNotFound", command.Id]));

        product.Name = command.Name;
        product.Description = command.Description;
        product.Price = command.Price;
        product.Categories = command.Categories;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

📁 `src/Modules/Catalog/Catalog/Products/Features/UpdateProduct/UpdateProductValidator.cs`

```csharp
using FluentValidation;
using Microsoft.Extensions.Localization;
using Catalog.Resources;

namespace Catalog.Products.Features.UpdateProduct;

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator(IStringLocalizer<CatalogMessages> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer["ProductNameRequired"]);

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage(localizer["ProductPriceMustBePositive"]);
    }
}
```

📁 `src/Modules/Catalog/Catalog/Products/Features/UpdateProduct/UpdateProductEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Shared.Extensions;

namespace Catalog.Products.Features.UpdateProduct;

public record UpdateProductRequest(string Name, string Description, decimal Price, List<string> Categories);

public class UpdateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/products/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(
                id, request.Name, request.Description, request.Price, request.Categories);

            var result = await sender.Send(command);
            return result.ToProblemResult();
        })
        .WithName("UpdateProduct")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Products");
    }
}
```

### 1.9 Feature: DeleteProduct

📁 `src/Modules/Catalog/Catalog/Products/Features/DeleteProduct/DeleteProductHandler.cs`

```csharp
using Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Catalog.Resources;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.DeleteProduct;

public record DeleteProductCommand(Guid Id) : ICommand<Result>;

internal class DeleteProductHandler(CatalogDbContext context, IStringLocalizer<CatalogMessages> localizer)
    : ICommandHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(
        DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);

        if (product is null)
            return Result.Failure(
                Error.NotFound("Product.NotFound", localizer["ProductNotFound", command.Id]));

        context.Products.Remove(product);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

📁 `src/Modules/Catalog/Catalog/Products/Features/DeleteProduct/DeleteProductEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Shared.Extensions;

namespace Catalog.Products.Features.DeleteProduct;

public class DeleteProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/products/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteProductCommand(id));
            return result.ToProblemResult();
        })
        .WithName("DeleteProduct")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Products");
    }
}
```

### 1.10 Module Registration Extension

📁 `src/Modules/Catalog/Catalog/CatalogModule.cs`

```csharp
using Catalog.Data;
using Catalog.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Interceptors;
using Shared.Data.Seed;

namespace Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   .AddInterceptors(
                       sp.GetRequiredService<AuditableEntityInterceptor>(),
                       sp.GetRequiredService<DispatchDomainEventsInterceptor>());
        });

        services.AddScoped<IDataSeeder, CatalogDataSeeder>();
        return services;
    }

    public static async Task<IApplicationBuilder> UseCatalogModule(this IApplicationBuilder app)
    {
        // Apply migrations
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Database.MigrateAsync();

        // Seed
        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAllAsync();

        return app;
    }
}
```

> ⚠️ **IMPORTANTE:** Os interceptors são `internal`. Precisam ser registrados no DI.
> Isso será feito na Fase 2 (Program.cs).

---

## FASE 2 — Program.cs + Infra (PostgreSQL)

> **Objetivo:** Fazer tudo rodar de verdade
> **Tempo estimado:** ~10 min

### 2.1 NuGet packages necessários

```bash
# No projeto Catalog (e depois nos outros módulos)
dotnet add src/Modules/Catalog/Catalog/Catalog.csproj package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.11

# No projeto Api
dotnet add src/Bootstrapper/Api/Api.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.26
```

### 2.2 appsettings.json

📁 `src/Bootstrapper/Api/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=OrderManagementDb;Username=postgres;Password=postgres"
  },
  "AllowedHosts": "*"
}
```

### 2.3 Tornar interceptors `public` ou registrá-los

O `AuditableEntityInterceptor` é `internal`. Para que os módulos possam usá-lo via DI,
registre no Shared ou torne público. A forma mais limpa:

📁 Alterar em `src/Shared/Shared/Data/Interceptors/AuditableEntityInterceptor.cs`:

```csharp
// Mudar de:
internal class AuditableEntityInterceptor(...)
// Para:
public class AuditableEntityInterceptor(...)
```

### 2.4 Program.cs completo

📁 `src/Bootstrapper/Api/Program.cs`

```csharp
using System.Globalization;
using Carter;
using Catalog;
using Shared.Data.Interceptors;
using Shared.Exceptions.Handler;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// --- CROSS-CUTTING ---
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();

// i18n
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("pt-BR"),
        new CultureInfo("es")
    };
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});

// --- SHARED INTERCEPTORS (registrados uma vez, usados por todos os módulos) ---
builder.Services.AddSingleton<AuditableEntityInterceptor>();
builder.Services.AddSingleton<DispatchDomainEventsInterceptor>();

// --- CARTER + MEDIATR (scan all module assemblies) ---
var catalogAssembly = typeof(CatalogModule).Assembly;
// var basketAssembly = typeof(BasketModule).Assembly;    // Fase 3
// var ordersAssembly = typeof(OrdersModule).Assembly;    // Fase 4

builder.Services.AddCarterWithAssemblies(catalogAssembly);
builder.Services.AddMediatRWithAssemblies(catalogAssembly);

// --- MODULES ---
builder.Services.AddCatalogModule(builder.Configuration);
// builder.Services.AddBasketModule(builder.Configuration);  // Fase 3
// builder.Services.AddOrdersModule(builder.Configuration);  // Fase 4

var app = builder.Build();

// --- MIDDLEWARE PIPELINE ---
app.UseExceptionHandler();
app.UseRequestLocalization();
app.UseHttpsRedirection();

// --- MAP ENDPOINTS ---
app.MapCarter();

// --- MODULE INITIALIZATION ---
await app.UseCatalogModule();
// await app.UseBasketModule();   // Fase 3
// await app.UseOrdersModule();   // Fase 4

app.Run();
```

### 2.5 Gerar migrations

```bash
dotnet ef migrations add InitialCatalog \
    --project src/Modules/Catalog/Catalog/Catalog.csproj \
    --startup-project src/Bootstrapper/Api/Api.csproj \
    --output-dir Data/Migrations
```

### 2.6 ✅ Checkpoint — Testar

```bash
# Subir PostgreSQL (Docker)
docker run --name pg-orders -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16

# Rodar
dotnet run --project src/Bootstrapper/Api/Api.csproj

# Testar
curl -X POST https://localhost:5001/api/products \
  -H "Content-Type: application/json" \
  -H "Accept-Language: pt-BR" \
  -d '{"name":"","description":"Test","price":-1,"categories":["Test"]}'

# Deve retornar 400 com mensagens em pt-BR
```

---

## FASE 3 — Módulo Basket (Redis Cache)

> **Objetivo:** Mostrar integração com cache distribuído (Redis)
> **Tempo estimado:** ~15 min

### 3.1 NuGet package

```bash
dotnet add src/Modules/Basket/Basket/Basket.csproj package Microsoft.Extensions.Caching.StackExchangeRedis --version 8.0.11
```

### 3.2 Entity — `ShoppingCart`

📁 `src/Modules/Basket/Basket/Basket/Models/ShoppingCart.cs`

```csharp
namespace Basket.Basket.Models;

public class ShoppingCart
{
    public string UserName { get; set; } = default!;
    public List<ShoppingCartItem> Items { get; set; } = [];
    public decimal TotalPrice => Items.Sum(i => i.Price * i.Quantity);
}

public class ShoppingCartItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
```

### 3.3 Repository pattern com Redis

📁 `src/Modules/Basket/Basket/Basket/Data/IBasketRepository.cs`

```csharp
using Basket.Basket.Models;

namespace Basket.Basket.Data;

public interface IBasketRepository
{
    Task<ShoppingCart?> GetBasketAsync(string userName, CancellationToken ct = default);
    Task<ShoppingCart> StoreBasketAsync(ShoppingCart cart, CancellationToken ct = default);
    Task<bool> DeleteBasketAsync(string userName, CancellationToken ct = default);
}
```

📁 `src/Modules/Basket/Basket/Basket/Data/BasketRepository.cs`

```csharp
using System.Text.Json;
using Basket.Basket.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace Basket.Basket.Data;

public class BasketRepository(IDistributedCache cache) : IBasketRepository
{
    public async Task<ShoppingCart?> GetBasketAsync(string userName, CancellationToken ct = default)
    {
        var data = await cache.GetStringAsync(userName, ct);
        return data is null ? null : JsonSerializer.Deserialize<ShoppingCart>(data);
    }

    public async Task<ShoppingCart> StoreBasketAsync(ShoppingCart cart, CancellationToken ct = default)
    {
        await cache.SetStringAsync(cart.UserName, JsonSerializer.Serialize(cart), ct);
        return cart;
    }

    public async Task<bool> DeleteBasketAsync(string userName, CancellationToken ct = default)
    {
        await cache.RemoveAsync(userName, ct);
        return true;
    }
}
```

### 3.4 Features: GetBasket, StoreBasket, DeleteBasket

> Mesmo padrão do Catalog: Command/Query → Handler → Endpoint.
> Handlers usam `IBasketRepository` em vez de DbContext.
> Erros usam `IStringLocalizer<BasketMessages>` com as chaves existentes nos .resx.

### 3.5 Module Registration

📁 `src/Modules/Basket/Basket/BasketModule.cs`

```csharp
using Basket.Basket.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basket;

public static class BasketModule
{
    public static IServiceCollection AddBasketModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });

        services.AddScoped<IBasketRepository, BasketRepository>();
        return services;
    }

    public static IApplicationBuilder UseBasketModule(this IApplicationBuilder app)
    {
        return app;
    }
}
```

### 3.6 appsettings.json — adicionar Redis

```json
"ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=OrderManagementDb;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
}
```

---

## FASE 4 — Módulo Orders (DDD + Domain Events)

> **Objetivo:** Mostrar DDD real com Aggregate, Domain Events e state machine
> **Tempo estimado:** ~15 min

### 4.1 Entities

📁 `src/Modules/Orders/Orders/Orders/Models/Order.cs`

```csharp
using Shared.DDD;

namespace Orders.Orders.Models;

public class Order : Aggregate<Guid>
{
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public List<OrderItem> Items { get; set; } = [];
    public decimal TotalPrice => Items.Sum(i => i.Price * i.Quantity);

    public static Order Create(Guid customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Items = items,
            Status = OrderStatus.Pending
        };

        // Domain Event!
        order.AddDomainEvent(new OrderCreatedEvent(order.Id, customerId));
        return order;
    }

    public void Cancel()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot cancel order in status {Status}");

        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledEvent(Id));
    }
}

public enum OrderStatus { Pending, Processing, Shipped, Delivered, Cancelled }
```

📁 `src/Modules/Orders/Orders/Orders/Models/OrderItem.cs`

```csharp
using Shared.DDD;

namespace Orders.Orders.Models;

public class OrderItem : Entity<Guid>
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
```

### 4.2 Domain Events

📁 `src/Modules/Orders/Orders/Orders/Events/OrderCreatedEvent.cs`

```csharp
using Shared.DDD;

namespace Orders.Orders.Models;

public record OrderCreatedEvent(Guid OrderId, Guid CustomerId) : IDomainEvent;
public record OrderCancelledEvent(Guid OrderId) : IDomainEvent;
```

### 4.3 Domain Event Handler

📁 `src/Modules/Orders/Orders/Orders/Events/OrderCreatedEventHandler.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Orders.Orders.Models;

namespace Orders.Orders.Events;

public class OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    : INotificationHandler<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: Order {OrderId} created for customer {CustomerId}",
            notification.OrderId, notification.CustomerId);

        // Aqui pode publicar mensagem no RabbitMQ (Fase 5)
        return Task.CompletedTask;
    }
}
```

### 4.4 DbContext, Features, Module Registration

> Mesmo padrão do Catalog:
> - `OrdersDbContext` com schema `"orders"`
> - CreateOrder, GetOrderById, CancelOrder
> - `OrdersModule.cs` com `AddOrdersModule` / `UseOrdersModule`

---

## FASE 5 — Messaging (RabbitMQ via MassTransit)

> **Objetivo:** Mostrar comunicação assíncrona entre módulos
> **Tempo estimado:** ~10 min

### 5.1 Integration Event (em Shared.Contracts)

📁 `src/Shared/Shared.Contracts/Messaging/OrderCreatedIntegrationEvent.cs`

```csharp
namespace Shared.Contracts.Messaging;

public record OrderCreatedIntegrationEvent(Guid OrderId, Guid CustomerId, decimal TotalPrice);
```

### 5.2 MassTransit configuration

📁 `src/Shared/Shared.Messaging/MessagingExtensions.cs`

```csharp
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Shared.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services, IConfiguration configuration, params Assembly[] consumerAssemblies)
    {
        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            bus.AddConsumers(consumerAssemblies);

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
```

### 5.3 Publisher (no OrderCreatedEventHandler)

```csharp
// No handler do domain event, publicar o integration event:
public class OrderCreatedEventHandler(
    ILogger<OrderCreatedEventHandler> logger,
    IPublishEndpoint publishEndpoint)
    : INotificationHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent notification, CancellationToken ct)
    {
        logger.LogInformation("Publishing integration event for Order {OrderId}", notification.OrderId);

        await publishEndpoint.Publish(
            new OrderCreatedIntegrationEvent(notification.OrderId, notification.CustomerId, 0),
            ct);
    }
}
```

### 5.4 Consumer (no módulo Basket — limpar carrinho após checkout)

📁 `src/Modules/Basket/Basket/Basket/Consumers/OrderCreatedConsumer.cs`

```csharp
using Basket.Basket.Data;
using MassTransit;
using Shared.Contracts.Messaging;

namespace Basket.Basket.Consumers;

public class OrderCreatedConsumer(IBasketRepository repository) : IConsumer<OrderCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        // Limpa o carrinho após o pedido ser criado
        await repository.DeleteBasketAsync(context.Message.CustomerId.ToString());
    }
}
```

### 5.5 appsettings.json — adicionar RabbitMQ

```json
"ConnectionStrings": {
    "DefaultConnection": "...",
    "Redis": "localhost:6379",
    "RabbitMQ": "amqp://guest:guest@localhost:5672"
}
```

---

## FASE 6 — Testes

> **Objetivo:** Provar que o core funciona
> **Tempo estimado:** ~10 min

### 6.1 Testes do Result Pattern

📁 `tests/OrderManagementApi.Tests/Results/ResultTests.cs`

```csharp
using Shared.Contracts.Results;

namespace OrderManagementApi.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_Result_Should_Have_No_Error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Failure_Result_Should_Have_Error()
    {
        var error = Error.NotFound("Test.NotFound", "Not found");
        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal("Test.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void Success_ResultT_Should_Return_Value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ResultT_Should_Throw_On_Value_Access()
    {
        var result = Result.Failure<int>(Error.Unexpected("Err", "fail"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Implicit_Conversion_Should_Create_Success()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Implicit_Conversion_Null_Should_Create_Failure()
    {
        Result<string> result = (string)null!;

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unexpected, result.Error.Type);
    }
}
```

### 6.2 Testes do Error

📁 `tests/OrderManagementApi.Tests/Results/ErrorTests.cs`

```csharp
using Shared.Contracts.Results;

namespace OrderManagementApi.Tests.Results;

public class ErrorTests
{
    [Theory]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void Error_Type_Should_Map_To_Correct_Value(ErrorType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    [Fact]
    public void Factory_Methods_Should_Set_Correct_Type()
    {
        Assert.Equal(ErrorType.NotFound, Error.NotFound("c", "m").Type);
        Assert.Equal(ErrorType.Validation, Error.Validation("c", "m").Type);
        Assert.Equal(ErrorType.BadRequest, Error.BadRequest("c", "m").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("c", "m").Type);
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("c", "m").Type);
        Assert.Equal(ErrorType.Unexpected, Error.Unexpected("c", "m").Type);
    }
}
```

---

## 📁 Estrutura final de pastas

```
src/
├── Bootstrapper/
│   └── Api/
│       ├── Program.cs                       ← Fase 2
│       ├── appsettings.json                 ← Fase 2
│       └── Api.csproj
│
├── Modules/
│   ├── Catalog/
│   │   └── Catalog/
│   │       ├── Products/
│   │       │   ├── Models/
│   │       │   │   └── Product.cs           ← Fase 1
│   │       │   └── Features/
│   │       │       ├── CreateProduct/       ← Fase 1
│   │       │       │   ├── CreateProductHandler.cs
│   │       │       │   ├── CreateProductValidator.cs
│   │       │       │   └── CreateProductEndpoint.cs
│   │       │       ├── GetProducts/         ← Fase 1
│   │       │       ├── GetProductById/      ← Fase 1
│   │       │       ├── UpdateProduct/       ← Fase 1
│   │       │       └── DeleteProduct/       ← Fase 1
│   │       ├── Data/
│   │       │   ├── CatalogDbContext.cs      ← Fase 1
│   │       │   ├── Configurations/          ← Fase 1
│   │       │   ├── Migrations/              ← Fase 2 (gerado)
│   │       │   └── Seed/                    ← Fase 1
│   │       ├── Resources/ (já existe)
│   │       └── CatalogModule.cs             ← Fase 1
│   │
│   ├── Basket/
│   │   └── Basket/
│   │       ├── Basket/
│   │       │   ├── Models/                  ← Fase 3
│   │       │   ├── Data/                    ← Fase 3 (Redis)
│   │       │   ├── Features/                ← Fase 3
│   │       │   └── Consumers/               ← Fase 5
│   │       ├── Resources/ (já existe)
│   │       └── BasketModule.cs              ← Fase 3
│   │
│   └── Orders/
│       └── Orders/
│           ├── Orders/
│           │   ├── Models/                  ← Fase 4
│           │   ├── Events/                  ← Fase 4
│           │   ├── Data/                    ← Fase 4
│           │   └── Features/                ← Fase 4
│           ├── Resources/ (já existe)
│           └── OrdersModule.cs              ← Fase 4
│
├── Shared/
│   ├── Shared/              (✅ PRONTO — core)
│   ├── Shared.Contracts/    (✅ PRONTO — core)
│   └── Shared.Messaging/
│       └── MessagingExtensions.cs           ← Fase 5
│
tests/
└── OrderManagementApi.Tests/
    └── Results/                             ← Fase 6
        ├── ResultTests.cs
        └── ErrorTests.cs
```

---

## 🐳 Docker Compose (infraestrutura local)

📁 `docker-compose.yml` (na raiz)

```yaml
version: '3.8'
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: OrderManagementDb
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"  # Management UI

volumes:
  pgdata:
```

---

## 🔁 Ordem de prática (Speed Run)

Para a entrevista, pratique nesta ordem até fazer em < 45 min:

```
1. docker compose up -d
2. Fase 1 — Catalog (Product, DbContext, CRUD features)
3. Fase 2 — Program.cs, appsettings, migration, RUN e TESTAR
4. Fase 3 — Basket (ShoppingCart, Redis, features)
5. Fase 4 — Orders (Aggregate, Domain Events, features)
6. Fase 5 — Messaging (integration event, consumer)
7. Fase 6 — Testes rápidos do core
```

### Atalhos de velocidade

| Ação | Dica |
|------|------|
| Criar feature | Copie CreateProduct e renomeie — o padrão é sempre Handler+Validator+Endpoint |
| DbContext | Copie CatalogDbContext, mude schema e entity |
| Module.cs | Copie CatalogModule.cs e adapte |
| Testes | Copie ResultTests e adapte para cada cenário |

---

## ✅ Checklist de validação na entrevista

- [ ] `POST /api/products` com body inválido → 400 + ProblemDetails + i18n
- [ ] `GET /api/products/{id}` com ID inexistente → 404 + ProblemDetails
- [ ] `Accept-Language: pt-BR` → mensagens em português
- [ ] `Accept-Language: es` → mensagens em espanhol
- [ ] CreatedAt/CreatedBy preenchidos automaticamente
- [ ] Domain Event `OrderCreatedEvent` logado no console
- [ ] Redis armazenando basket (verificar no `redis-cli`)
- [ ] RabbitMQ recebendo mensagem (verificar no management UI :15672)
