# 🚀 FASE 1 — CATALOG MODULE (SPEEDRUN GUIDE)

> **Objetivo:** Implementar CRUD completo com CQRS + EF Core + Carter + FluentValidation + i18n
> **Tempo alvo:** ~40 min (primeira vez) → ~15 min (depois de decorar)
> **Pré-requisito:** PostgreSQL rodando em localhost:5432 (user: postgres, pass: postgres)

---

## CHECKLIST RÁPIDO (risque conforme faz)

```
[ ] STEP 1  — Catalog.csproj (FrameworkReference)
[ ] STEP 2  — Product.cs (Entidade)
[ ] STEP 3  — ProductConfiguration.cs (EF Fluent Config)
[ ] STEP 4  — CatalogDbContext.cs
[ ] STEP 5  — AuditableEntityInterceptor → trocar internal por public
[ ] STEP 6  — CatalogModule.cs (DI do módulo)
[ ] STEP 7  — appsettings.json (ConnectionString)
[ ] STEP 8  — Program.cs (registrar tudo)
[ ] STEP 9  — Add-Migration + Rodar API (confirmar banco criou)
[ ] STEP 10 — CreateProduct (Command + Handler + Validator + Endpoint)
[ ] STEP 11 — GetProducts (Query + Handler + Endpoint — paginado)
[ ] STEP 12 — GetProductById (Query + Handler + Endpoint)
[ ] STEP 13 — UpdateProduct (Command + Handler + Validator + Endpoint)
[ ] STEP 14 — DeleteProduct (Command + Handler + Endpoint)
[ ] STEP 15 — CatalogDataSeeder
[ ] STEP 16 — Registrar Seeder no CatalogModule
[ ] STEP 17 — Testar TODOS os endpoints
[ ] STEP 18 — git commit
```

---

## STEP 1 — Catalog.csproj

**Por quê:** Carter usa `IEndpointRouteBuilder` que vem do ASP.NET Core. Sem isso, endpoints não compilam.

**Arquivo:** `src/Modules/Catalog/Catalog/Catalog.csproj`

**Adicionar** o bloco `FrameworkReference` dentro do `<Project>`:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

**Estado final do .csproj:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Shared\Shared.csproj" />
    <ProjectReference Include="..\..\..\Shared\Shared.Contracts\Shared.Contracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.*" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

---

## STEP 2 — Entidade Product

**Por quê:** `Entity<Guid>` já herda `Id`, `CreatedAt`, `CreatedBy`, `LastModified`, `LastModifiedBy` do Shared. Você só adiciona as propriedades de negócio.

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Product.cs`

```csharp
using Shared.DDD;

namespace Catalog.Products;

public class Product : Entity<Guid>
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
}
```

> **Nota:** `Entity<Guid>` e não `Aggregate<Guid>` porque Catalog é simples — não emite Domain Events. Reserve `Aggregate` para Orders.

---

## STEP 3 — ProductConfiguration

**Por quê:** Fluent API é mais explícita e testável que Data Annotations. Configuração separada por entidade = organização.

**Arquivo:** `src/Modules/Catalog/Catalog/Data/Configurations/ProductConfiguration.cs`

```csharp
using Catalog.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
    }
}
```

---

## STEP 4 — CatalogDbContext

**Por quê:** Cada módulo tem seu próprio `DbContext` com schema isolado. Isso é o padrão de modular monolith — módulos não compartilham tabelas.

**Arquivo:** `src/Modules/Catalog/Catalog/Data/CatalogDbContext.cs`

```csharp
using Catalog.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("catalog");
        builder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
```

> **`HasDefaultSchema("catalog")`** — cria todas as tabelas dentro do schema `catalog` no PostgreSQL. Orders terá `orders`, Basket usa Redis (sem schema).

---

## STEP 5 — Tornar AuditableEntityInterceptor público

**Por quê:** É `internal` — o `CatalogModule` precisa resolvê-lo via DI, e o `Program.cs` precisa registrá-lo. `internal` impede isso.

**Arquivo:** `src/Shared/Shared/Data/Interceptors/AuditableEntityInterceptor.cs`

**Trocar** na linha 10:

```csharp
// DE:
internal class AuditableEntityInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor

// PARA:
public class AuditableEntityInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
```

> **Apenas** trocar `internal` por `public`. Nada mais.

---

## STEP 6 — CatalogModule

**Por quê:** O módulo encapsula todo o seu DI. O `Program.cs` chama `AddCatalogModule()` e não precisa saber detalhes internos.

**Arquivo:** `src/Modules/Catalog/Catalog/CatalogModule.cs`

```csharp
using Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Interceptors;

namespace Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>());

            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"));
        });

        return services;
    }
}
```

> **Interceptors via DI:** `sp.GetRequiredService<>()` resolve do container. O `AuditableEntityInterceptor` precisa de `IHttpContextAccessor`, o `DispatchDomainEventsInterceptor` precisa de `IMediator` — o DI cuida disso.

---

## STEP 7 — ConnectionString

**Arquivo:** `src/Bootstrapper/Api/appsettings.json`

**Adicionar** `ConnectionStrings` no JSON:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=OrderManagementDb;Username=postgres;Password=postgres"
  }
}
```

> Ajuste `Username` e `Password` pro seu PostgreSQL local.

---

## STEP 8 — Program.cs

**Por quê:** É aqui que tudo se conecta. A ordem importa.

**Arquivo:** `src/Bootstrapper/Api/Program.cs`

**Estado final:**

```csharp
using System.Globalization;
using Carter;
using Catalog;
using Catalog.Data;
using Shared.Data.Interceptors;
using Shared.Exceptions.Handler;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Safety net: catches any unhandled exceptions and returns ProblemDetails with i18n
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();

// i18n: Register localization services
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

// CQRS + Validation + Logging pipeline
var catalogAssembly = typeof(CatalogModule).Assembly;
builder.Services.AddMediatRWithAssemblies(catalogAssembly);

// Carter endpoints
builder.Services.AddCarterWithAssemblies(catalogAssembly);

// EF Core interceptors (resolved via DI)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditableEntityInterceptor>();
builder.Services.AddScoped<DispatchDomainEventsInterceptor>();

// Modules
builder.Services.AddCatalogModule(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.

// Safety net: must be before other middleware to catch unhandled exceptions
app.UseExceptionHandler();

// i18n: Enable request localization middleware (must be before endpoints)
app.UseRequestLocalization();

app.UseHttpsRedirection();

// Carter: map all module endpoints
app.MapCarter();

// Auto-migrate + seed on startup
await app.UseMigrationAsync<CatalogDbContext>();

app.Run();
```

**O que foi adicionado (em relação ao original):**

| Linha | O quê | Por quê |
|---|---|---|
| `using Carter;` | Namespace do Carter | `MapCarter()` |
| `using Catalog;` | Namespace do módulo | `CatalogModule`, `typeof(CatalogModule).Assembly` |
| `using Catalog.Data;` | Namespace do DbContext | `UseMigrationAsync<CatalogDbContext>()` |
| `using Shared.Data.Interceptors;` | Interceptors | Registrar no DI |
| `using Shared.Extensions;` | Extensions | `AddMediatRWithAssemblies`, `AddCarterWithAssemblies`, `UseMigrationAsync` |
| `AddMediatRWithAssemblies` | Registra MediatR + handlers + validators + behaviors | Scan automático do assembly |
| `AddCarterWithAssemblies` | Registra endpoints Carter | Scan automático do assembly |
| `AddHttpContextAccessor` | `IHttpContextAccessor` no DI | `AuditableEntityInterceptor` precisa |
| `AddScoped<Interceptors>` | Interceptors no DI | `CatalogModule` resolve via `sp.GetRequiredService<>()` |
| `AddCatalogModule` | DI do módulo | DbContext + Npgsql + Interceptors |
| `MapCarter()` | Ativa endpoints | Sem isso nenhuma rota funciona |
| `UseMigrationAsync` | Auto-migrate + seed | Aplica migrations pendentes no startup |

---

## STEP 9 — Migration

**No Package Manager Console (Visual Studio):**

```
Add-Migration InitialCatalog -Project Catalog -StartupProject Api -OutputDir Data/Migrations
```

**OU via CLI (terminal):**

```bash
dotnet ef migrations add InitialCatalog --project src/Modules/Catalog/Catalog --startup-project src/Bootstrapper/Api --output-dir Data/Migrations
```

**Depois: rodar a API (F5).** Confirme que:
- Banco `OrderManagementDb` foi criado no PostgreSQL
- Schema `catalog` existe
- Tabela `catalog.Products` existe com todas as colunas (incluindo `CreatedAt`, `CreatedBy`, etc. do `Entity<T>`)

> **Se der erro de conexão:** verifique se o PostgreSQL está rodando e se user/password estão corretos no `appsettings.json`.

---

## STEP 10 — CreateProduct

### 10.1 — Command

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/CreateProduct/CreateProductCommand.cs`

```csharp
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.CreateProduct;

public record CreateProductCommand(
    string Name, string? Description, string? ImageUrl, decimal Price)
    : ICommand<Result<Guid>>;
```

> **`ICommand<Result<Guid>>`** porque retorna o Id do produto criado. Se fosse operação sem retorno, seria `ICommand` (que é alias de `ICommand<Result>`).

### 10.2 — Handler

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/CreateProduct/CreateProductHandler.cs`

```csharp
using Catalog.Data;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.CreateProduct;

public class CreateProductHandler(CatalogDbContext db)
    : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            ImageUrl = command.ImageUrl,
            Price = command.Price
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(product.Id);
    }
}
```

> **Primary constructor** `(CatalogDbContext db)` — DI automático. O `SaveChangesAsync` vai triggerar o `AuditableEntityInterceptor` que preenche `CreatedAt`, `CreatedBy`.

### 10.3 — Validator

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/CreateProduct/CreateProductValidator.cs`

```csharp
using Catalog.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

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

> **Não precisa registrar manualmente.** O `AddMediatRWithAssemblies` chama `AddValidatorsFromAssemblies` que descobre todos os validators. O `ValidationBehavior` intercepta automaticamente e retorna `Result.Failure(Error.Validation(...))` → vira 400 ProblemDetails.

### 10.4 — Endpoint

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/CreateProduct/CreateProductEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.CreateProduct;

public record CreateProductRequest(
    string Name, string? Description, string? ImageUrl, decimal Price);

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (
            CreateProductRequest request, ISender sender) =>
        {
            var command = new CreateProductCommand(
                request.Name, request.Description,
                request.ImageUrl, request.Price);

            var result = await sender.Send(command);

            return result.ToCreatedResult($"/api/products/{result.Value}");
        });
    }
}
```

> **Request ≠ Command.** O `CreateProductRequest` é o DTO HTTP (o que o cliente envia). O `CreateProductCommand` é o objeto de domínio que passa pelo pipeline MediatR. Sempre mapeie um pro outro.
>
> **`ToCreatedResult`** retorna 201 Created com header `Location: /api/products/{id}`.

---

## STEP 11 — GetProducts (paginação)

### 11.1 — Query + DTO

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/GetProducts/GetProductsQuery.cs`

```csharp
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;
using Shared.Pagination;

namespace Catalog.Products.Features.GetProducts;

public record GetProductsQuery(int PageIndex = 0, int PageSize = 10)
    : IQuery<Result<PaginatedResult<ProductDto>>>;

public record ProductDto(
    Guid Id, string Name, string? Description, string? ImageUrl, decimal Price);
```

> **`ProductDto`** — nunca exponha a entidade diretamente. O DTO controla o que o cliente vê.

### 11.2 — Handler

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/GetProducts/GetProductsHandler.cs`

```csharp
using Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;
using Shared.Pagination;

namespace Catalog.Products.Features.GetProducts;

public class GetProductsHandler(CatalogDbContext db)
    : IQueryHandler<GetProductsQuery, Result<PaginatedResult<ProductDto>>>
{
    public async Task<Result<PaginatedResult<ProductDto>>> Handle(
        GetProductsQuery query, CancellationToken cancellationToken)
    {
        var count = await db.Products.LongCountAsync(cancellationToken);

        var products = await db.Products
            .OrderBy(p => p.Name)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description, p.ImageUrl, p.Price))
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<ProductDto>(
            query.PageIndex, query.PageSize, count, products);

        return Result.Success(result);
    }
}
```

> **`Select` direto na query** — projeta para DTO no SQL. Não traz a entidade inteira pra memória. Performance.

### 11.3 — Endpoint

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/GetProducts/GetProductsEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.GetProducts;

public class GetProductsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", async (
            int pageIndex, int pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetProductsQuery(pageIndex, pageSize));

            return result.ToProblemResult();
        });
    }
}
```

> **`ToProblemResult()`** — sucesso retorna 200 OK com o body. Falha retorna ProblemDetails.

---

## STEP 12 — GetProductById

### 12.1 — Query

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/GetProductById/GetProductByIdQuery.cs`

```csharp
using Catalog.Products.Features.GetProducts;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.GetProductById;

public record GetProductByIdQuery(Guid Id)
    : IQuery<Result<ProductDto>>;
```

> Reutiliza o `ProductDto` do `GetProducts` — não duplique DTOs iguais.

### 12.2 — Handler

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/GetProductById/GetProductByIdHandler.cs`

```csharp
using Catalog.Data;
using Catalog.Products.Features.GetProducts;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.GetProductById;

public class GetProductByIdHandler(
    CatalogDbContext db,
    IStringLocalizer<CatalogMessages> localizer)
    : IQueryHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([query.Id], cancellationToken);

        if (product is null)
            return Result.Failure<ProductDto>(
                Error.NotFound("Product.NotFound", localizer["ProductNotFound", query.Id]));

        var dto = new ProductDto(
            product.Id, product.Name, product.Description, product.ImageUrl, product.Price);

        return Result.Success(dto);
    }
}
```

> **`Error.NotFound("Product.NotFound", localizer["ProductNotFound", query.Id])`**
> - `"Product.NotFound"` = Code (estável, não muda por idioma)
> - `localizer["ProductNotFound", query.Id]` = Message (i18n, com parâmetro `{0}`)
> - Isso gera: `"Product with ID "abc" was not found."` em en-US, ou a versão traduzida em pt-BR/es

### 12.3 — Endpoint

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/GetProductById/GetProductByIdEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.GetProductById;

public class GetProductByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:guid}", async (
            Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProductByIdQuery(id));

            return result.ToProblemResult();
        });
    }
}
```

> **`{id:guid}`** — route constraint. Se alguém passar `/api/products/abc`, retorna 404 antes mesmo de chegar no handler.

---

## STEP 13 — UpdateProduct

### 13.1 — Command

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/UpdateProduct/UpdateProductCommand.cs`

```csharp
using Shared.Contracts.CQRS;

namespace Catalog.Products.Features.UpdateProduct;

public record UpdateProductCommand(
    Guid Id, string Name, string? Description, string? ImageUrl, decimal Price)
    : ICommand;
```

> **`ICommand`** (sem genérico) = `ICommand<Result>` = operação sem valor de retorno. Sucesso é 204 No Content.

### 13.2 — Handler

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/UpdateProduct/UpdateProductHandler.cs`

```csharp
using Catalog.Data;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.UpdateProduct;

public class UpdateProductHandler(
    CatalogDbContext db,
    IStringLocalizer<CatalogMessages> localizer)
    : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(
        UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([command.Id], cancellationToken);

        if (product is null)
            return Result.Failure(
                Error.NotFound("Product.NotFound", localizer["ProductNotFound", command.Id]));

        product.Name = command.Name;
        product.Description = command.Description;
        product.ImageUrl = command.ImageUrl;
        product.Price = command.Price;

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

> **`ICommandHandler<UpdateProductCommand>`** (sem segundo genérico) = handler retorna `Result`. O `AuditableEntityInterceptor` preenche `LastModified`, `LastModifiedBy` automaticamente no `SaveChangesAsync`.

### 13.3 — Validator

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/UpdateProduct/UpdateProductValidator.cs`

```csharp
using Catalog.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Catalog.Products.Features.UpdateProduct;

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator(IStringLocalizer<CatalogMessages> localizer)
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localizer["ProductNameRequired"]);

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage(localizer["ProductPriceMustBePositive"]);
    }
}
```

### 13.4 — Endpoint

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/UpdateProduct/UpdateProductEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.UpdateProduct;

public record UpdateProductRequest(
    string Name, string? Description, string? ImageUrl, decimal Price);

public class UpdateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/products/{id:guid}", async (
            Guid id, UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(
                id, request.Name, request.Description,
                request.ImageUrl, request.Price);

            var result = await sender.Send(command);

            return result.ToProblemResult();
        });
    }
}
```

> **Id vem da ROTA, não do body.** O `UpdateProductRequest` NÃO tem `Id`. Isso é REST correto: `PUT /api/products/{id}`.

---

## STEP 14 — DeleteProduct

### 14.1 — Command

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/DeleteProduct/DeleteProductCommand.cs`

```csharp
using Shared.Contracts.CQRS;

namespace Catalog.Products.Features.DeleteProduct;

public record DeleteProductCommand(Guid Id) : ICommand;
```

### 14.2 — Handler

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/DeleteProduct/DeleteProductHandler.cs`

```csharp
using Catalog.Data;
using Catalog.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Catalog.Products.Features.DeleteProduct;

public class DeleteProductHandler(
    CatalogDbContext db,
    IStringLocalizer<CatalogMessages> localizer)
    : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(
        DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([command.Id], cancellationToken);

        if (product is null)
            return Result.Failure(
                Error.NotFound("Product.NotFound", localizer["ProductNotFound", command.Id]));

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

### 14.3 — Endpoint

**Arquivo:** `src/Modules/Catalog/Catalog/Products/Features/DeleteProduct/DeleteProductEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Catalog.Products.Features.DeleteProduct;

public class DeleteProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/products/{id:guid}", async (
            Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteProductCommand(id));

            return result.ToProblemResult();
        });
    }
}
```

> **Delete não precisa de Validator** — só o Id que já vem validado pela route constraint `{id:guid}`.

---

## STEP 15 — CatalogDataSeeder

**Arquivo:** `src/Modules/Catalog/Catalog/Data/Seed/CatalogDataSeeder.cs`

```csharp
using Catalog.Products;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;

namespace Catalog.Data.Seed;

public class CatalogDataSeeder(CatalogDbContext db) : IDataSeeder
{
    public async Task SeedAllAsync()
    {
        if (await db.Products.AnyAsync())
            return;

        var products = new List<Product>
        {
            new() { Id = Guid.NewGuid(), Name = "iPhone 15 Pro", Description = "Apple smartphone", Price = 999.99m },
            new() { Id = Guid.NewGuid(), Name = "Samsung Galaxy S24", Description = "Samsung flagship", Price = 849.99m },
            new() { Id = Guid.NewGuid(), Name = "MacBook Pro 14\"", Description = "Apple laptop M3 Pro", Price = 1999.99m },
            new() { Id = Guid.NewGuid(), Name = "Sony WH-1000XM5", Description = "Noise cancelling headphones", Price = 349.99m },
            new() { Id = Guid.NewGuid(), Name = "Logitech MX Master 3S", Description = "Ergonomic wireless mouse", Price = 99.99m }
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }
}
```

> **Idempotente:** `if (await db.Products.AnyAsync()) return;` — roda várias vezes sem duplicar dados.

---

## STEP 16 — Registrar Seeder no CatalogModule

**Arquivo:** `src/Modules/Catalog/Catalog/CatalogModule.cs`

**Adicionar** no topo dos usings:

```csharp
using Catalog.Data.Seed;
using Shared.Data.Seed;
```

**Adicionar** antes do `return services;`:

```csharp
services.AddScoped<IDataSeeder, CatalogDataSeeder>();
```

**Estado final:**

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
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>());

            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"));
        });

        services.AddScoped<IDataSeeder, CatalogDataSeeder>();

        return services;
    }
}
```

---

## STEP 17 — Testar

**Rodar a API (F5)** e testar todos os endpoints:

```
✅ POST   /api/products                           → 201 Created + body com Guid
✅ POST   /api/products (sem nome)                → 400 ProblemDetails (validation)
✅ POST   /api/products (preço 0)                 → 400 ProblemDetails (validation)
✅ GET    /api/products?pageIndex=0&pageSize=10   → 200 OK + PaginatedResult
✅ GET    /api/products/{id-existente}             → 200 OK + ProductDto
✅ GET    /api/products/{id-inexistente}           → 404 ProblemDetails
✅ GET    /api/products/abc                        → 404 (route constraint)
✅ PUT    /api/products/{id}                       → 204 No Content
✅ PUT    /api/products/{id} (sem nome)            → 400 ProblemDetails
✅ PUT    /api/products/{id-inexistente}           → 404 ProblemDetails
✅ DELETE /api/products/{id}                       → 204 No Content
✅ DELETE /api/products/{id} (de novo)             → 404 ProblemDetails
```

> **Teste de i18n:** adicione header `Accept-Language: pt-BR` e as mensagens de erro vêm em português.

---

## STEP 18 — Commit

```bash
git add -A
git commit -m "feat: implement Catalog module - full CRUD with CQRS, EF Core, Carter, FluentValidation, i18n"
```

---

## 📁 ESTRUTURA FINAL DO CATALOG

```
src/Modules/Catalog/Catalog/
├── CatalogModule.cs
├── Catalog.csproj
├── Data/
│   ├── CatalogDbContext.cs
│   ├── Configurations/
│   │   └── ProductConfiguration.cs
│   ├── Migrations/
│   │   ├── XXXXXXXX_InitialCatalog.cs
│   │   ├── XXXXXXXX_InitialCatalog.Designer.cs
│   │   └── CatalogDbContextModelSnapshot.cs
│   └── Seed/
│       └── CatalogDataSeeder.cs
├── Products/
│   ├── Product.cs
│   └── Features/
│       ├── CreateProduct/
│       │   ├── CreateProductCommand.cs
│       │   ├── CreateProductHandler.cs
│       │   ├── CreateProductValidator.cs
│       │   └── CreateProductEndpoint.cs
│       ├── GetProducts/
│       │   ├── GetProductsQuery.cs      (+ ProductDto aqui)
│       │   ├── GetProductsHandler.cs
│       │   └── GetProductsEndpoint.cs
│       ├── GetProductById/
│       │   ├── GetProductByIdQuery.cs
│       │   ├── GetProductByIdHandler.cs
│       │   └── GetProductByIdEndpoint.cs
│       ├── UpdateProduct/
│       │   ├── UpdateProductCommand.cs
│       │   ├── UpdateProductHandler.cs
│       │   ├── UpdateProductValidator.cs
│       │   └── UpdateProductEndpoint.cs
│       └── DeleteProduct/
│           ├── DeleteProductCommand.cs
│           ├── DeleteProductHandler.cs
│           └── DeleteProductEndpoint.cs
└── Resources/
    ├── CatalogMessages.cs               (já existia)
    ├── CatalogMessages.resx             (já existia)
    ├── CatalogMessages.pt-BR.resx       (já existia)
    └── CatalogMessages.es.resx          (já existia)
```

---

## 🔄 PARA REFAZER (SPEEDRUN RESET)

```bash
git stash -u
git checkout core-ready
git stash drop
```

Isso volta pro estado limpo. Refaça quantas vezes quiser.

---

## 🧠 COLA DE PADRÕES (decorar)

| Padrão | Interface | Retorno |
|---|---|---|
| Command com retorno | `ICommand<Result<T>>` | `ICommandHandler<TCmd, Result<T>>` |
| Command sem retorno | `ICommand` | `ICommandHandler<TCmd>` |
| Query | `IQuery<Result<T>>` | `IQueryHandler<TQuery, Result<T>>` |
| Sucesso com valor | `Result.Success(value)` | 200 OK |
| Sucesso sem valor | `Result.Success()` | 204 No Content |
| Sucesso criação | `result.ToCreatedResult(uri)` | 201 Created |
| Falha not found | `Result.Failure(Error.NotFound(...))` | 404 |
| Falha validation | automático via `ValidationBehavior` | 400 |

| Extension Method | Quando usar |
|---|---|
| `result.ToProblemResult()` | GET, PUT, DELETE |
| `result.ToProblemResult<T>()` | GET que retorna valor |
| `result.ToCreatedResult(uri)` | POST que cria recurso |
