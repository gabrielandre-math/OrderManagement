# 🚀 FASE 2 — BASKET MODULE (SPEEDRUN GUIDE)

> **Objetivo:** Implementar módulo com Redis (storage diferente do EF Core), Decorator Pattern, e publicação de Integration Event via MassTransit
> **Tempo alvo:** ~45 min (primeira vez) → ~20 min (depois de decorar)
> **Pré-requisitos:**
> - Fase 1 (Catalog) implementada e funcionando
> - Redis rodando em localhost:6379 (`docker run -d --name redis -p 6379:6379 redis`)
> - RabbitMQ rodando em localhost:5672 (`docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management`)

---

## CHECKLIST RÁPIDO

```
[ ] STEP 1  — Basket.csproj (pacotes + FrameworkReference)
[ ] STEP 2  — BasketMessages.cs → trocar internal por public
[ ] STEP 3  — ShoppingCartItem.cs (value object)
[ ] STEP 4  — ShoppingCart.cs (entidade)
[ ] STEP 5  — IBasketRepository.cs (interface)
[ ] STEP 6  — BasketRepository.cs (Redis via IDistributedCache)
[ ] STEP 7  — CachedBasketRepository.cs (Decorator Pattern)
[ ] STEP 8  — StoreBasket (Command + Handler + Validator + Endpoint)
[ ] STEP 9  — GetBasket (Query + Handler + Endpoint)
[ ] STEP 10 — DeleteBasket (Command + Handler + Endpoint)
[ ] STEP 11 — CheckoutBasket (Command + Handler + Validator + Endpoint)
[ ] STEP 12 — BasketModule.cs (DI do módulo)
[ ] STEP 13 — appsettings.json (Redis + RabbitMQ config)
[ ] STEP 14 — Program.cs (registrar Basket + MassTransit)
[ ] STEP 15 — Testar TODOS os endpoints
[ ] STEP 16 — git commit
```

---

## CONCEITOS-CHAVE DESTA FASE

| Conceito | O que é | Por que importa |
|---|---|---|
| **Redis como cache distribuído** | `IDistributedCache` — abstração do .NET, Redis é o provider | Basket não precisa de tabelas SQL — é temporário, key-value é perfeito |
| **Decorator Pattern** | Uma classe "envolve" outra adicionando comportamento | Cache em memória na frente do Redis — evita round-trip desnecessário |
| **Scrutor** | Biblioteca que facilita registrar Decorators no DI | `services.Decorate<IBasketRepository, CachedBasketRepository>()` |
| **Integration Event** | Evento publicado via mensageria (RabbitMQ) para outro módulo | Basket publica `BasketCheckoutIntegrationEvent` → Orders vai consumir na Fase 3 |
| **Sem EF Core** | Basket não tem DbContext, não tem migrations | Prova que a arquitetura é flexível — cada módulo escolhe seu storage |

---

## STEP 1 — Basket.csproj

**Por quê:** Precisa de Redis (StackExchangeRedis), Scrutor (Decorator), Shared.Messaging (Integration Events) e ASP.NET Core (Carter endpoints).

**Arquivo:** `src/Modules/Basket/Basket/Basket.csproj`

**Estado final:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Shared\Shared.csproj" />
    <ProjectReference Include="..\..\..\Shared\Shared.Contracts\Shared.Contracts.csproj" />
    <ProjectReference Include="..\..\..\Shared\Shared.Messaging\Shared.Messaging.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.26" />
    <PackageReference Include="Scrutor" Version="7.0.0" />
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

**O que mudou vs original:**

| Adição | Por quê |
|---|---|
| `Shared.Messaging` ProjectReference | Para acessar `BasketCheckoutIntegrationEvent` e `MassTransitExtensions` |
| `StackExchangeRedis 8.0.26` | Provider Redis para `IDistributedCache` |
| `Scrutor 7.0.0` | `services.Decorate<>()` para o Decorator Pattern |
| `FrameworkReference Microsoft.AspNetCore.App` | Carter endpoints (mesmo motivo do Catalog) |

---

## STEP 2 — BasketMessages.cs → public

**Por quê:** É `internal` — o `IStringLocalizer<BasketMessages>` precisa acessar de fora.

**Arquivo:** `src/Modules/Basket/Basket/Resources/BasketMessages.cs`

**Trocar:**

```csharp
// DE:
internal class BasketMessages

// PARA:
public class BasketMessages
```

---

## STEP 3 — ShoppingCartItem

**Por quê:** É um value object simples — não tem identidade própria, pertence ao `ShoppingCart`.

**Arquivo:** `src/Modules/Basket/Basket/Basket/ShoppingCartItem.cs`

```csharp
namespace Basket.Basket;

public class ShoppingCartItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
```

> **Sem herança de `Entity<T>`** — não tem Id próprio, não é auditável. É parte do ShoppingCart.

---

## STEP 4 — ShoppingCart

**Por quê:** Entidade principal do módulo. O `UserName` é a chave (cada usuário tem um carrinho).

**Arquivo:** `src/Modules/Basket/Basket/Basket/ShoppingCart.cs`

```csharp
namespace Basket.Basket;

public class ShoppingCart
{
    public string UserName { get; set; } = default!;
    public List<ShoppingCartItem> Items { get; set; } = [];

    public decimal TotalPrice => Items.Sum(i => i.Price * i.Quantity);
}
```

> **Não herda de `Entity<T>`** — não é persistida no EF Core. É serializada como JSON no Redis. O `TotalPrice` é computed (calculado em runtime, não armazenado).

---

## STEP 5 — IBasketRepository

**Por quê:** Abstração do storage. O handler não sabe se é Redis, SQL, ou arquivo. Facilita testes e permite o Decorator Pattern.

**Arquivo:** `src/Modules/Basket/Basket/Data/IBasketRepository.cs`

```csharp
using Basket.Basket;

namespace Basket.Data;

public interface IBasketRepository
{
    Task<ShoppingCart?> GetBasketAsync(string userName, CancellationToken ct = default);
    Task<ShoppingCart> StoreBasketAsync(ShoppingCart cart, CancellationToken ct = default);
    Task<bool> DeleteBasketAsync(string userName, CancellationToken ct = default);
}
```

---

## STEP 6 — BasketRepository (Redis)

**Por quê:** Implementação real que usa Redis via `IDistributedCache`. Serializa o carrinho como JSON.

**Arquivo:** `src/Modules/Basket/Basket/Data/BasketRepository.cs`

```csharp
using System.Text.Json;
using Basket.Basket;
using Microsoft.Extensions.Caching.Distributed;

namespace Basket.Data;

public class BasketRepository(IDistributedCache cache) : IBasketRepository
{
    public async Task<ShoppingCart?> GetBasketAsync(string userName, CancellationToken ct = default)
    {
        var json = await cache.GetStringAsync(userName, ct);

        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<ShoppingCart>(json);
    }

    public async Task<ShoppingCart> StoreBasketAsync(ShoppingCart cart, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(cart);

        await cache.SetStringAsync(cart.UserName, json, ct);

        return cart;
    }

    public async Task<bool> DeleteBasketAsync(string userName, CancellationToken ct = default)
    {
        await cache.RemoveAsync(userName, ct);

        return true;
    }
}
```

> **`IDistributedCache`** — abstração do .NET. O Redis é registrado como provider no DI. Se quiser trocar pra SQL Server cache ou NCache, muda só o registration, não o código.
>
> **Key = UserName** — cada carrinho é identificado pelo nome do usuário no Redis.

---

## STEP 7 — CachedBasketRepository (Decorator)

**Por quê:** O Redis já é rápido, mas fazer round-trip a cada request é desnecessário. O Decorator adiciona cache em memória (`IMemoryCache`) na frente do Redis. Se o carrinho já está na memória do processo, retorna direto sem ir ao Redis.

**Arquivo:** `src/Modules/Basket/Basket/Data/CachedBasketRepository.cs`

```csharp
using Basket.Basket;
using Microsoft.Extensions.Caching.Memory;

namespace Basket.Data;

public class CachedBasketRepository(
    IBasketRepository repository,
    IMemoryCache memoryCache) : IBasketRepository
{
    public async Task<ShoppingCart?> GetBasketAsync(string userName, CancellationToken ct = default)
    {
        if (memoryCache.TryGetValue(userName, out ShoppingCart? cart))
            return cart;

        cart = await repository.GetBasketAsync(userName, ct);

        if (cart is not null)
            memoryCache.Set(userName, cart, TimeSpan.FromMinutes(30));

        return cart;
    }

    public async Task<ShoppingCart> StoreBasketAsync(ShoppingCart cart, CancellationToken ct = default)
    {
        await repository.StoreBasketAsync(cart, ct);

        memoryCache.Set(cart.UserName, cart, TimeSpan.FromMinutes(30));

        return cart;
    }

    public async Task<bool> DeleteBasketAsync(string userName, CancellationToken ct = default)
    {
        await repository.DeleteBasketAsync(userName, ct);

        memoryCache.Remove(userName);

        return true;
    }
}
```

> **Como o Decorator funciona:**
> 1. `CachedBasketRepository` recebe `IBasketRepository` no construtor (que é o `BasketRepository` real)
> 2. Antes de ir ao Redis, checa o `IMemoryCache`
> 3. Se encontrou → retorna direto (zero latência)
> 4. Se não → vai ao Redis, e depois guarda no memory cache
>
> **O handler não sabe que o Decorator existe.** Ele injeta `IBasketRepository` e recebe o `CachedBasketRepository` transparentemente.

---

## STEP 8 — StoreBasket (criar/atualizar carrinho)

### 8.1 — Command

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/StoreBasket/StoreBasketCommand.cs`

```csharp
using Basket.Basket;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Basket.Basket.Features.StoreBasket;

public record StoreBasketCommand(ShoppingCart Cart)
    : ICommand<Result<ShoppingCart>>;
```

### 8.2 — Handler

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/StoreBasket/StoreBasketHandler.cs`

```csharp
using Basket.Data;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Basket.Basket.Features.StoreBasket;

public class StoreBasketHandler(IBasketRepository repository)
    : ICommandHandler<StoreBasketCommand, Result<ShoppingCart>>
{
    public async Task<Result<ShoppingCart>> Handle(
        StoreBasketCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.StoreBasketAsync(command.Cart, cancellationToken);

        return Result.Success(cart);
    }
}
```

### 8.3 — Validator

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/StoreBasket/StoreBasketValidator.cs`

```csharp
using Basket.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Basket.Basket.Features.StoreBasket;

public class StoreBasketValidator : AbstractValidator<StoreBasketCommand>
{
    public StoreBasketValidator(IStringLocalizer<BasketMessages> localizer)
    {
        RuleFor(x => x.Cart.UserName)
            .NotEmpty().WithMessage(localizer["UserNameRequired"]);

        RuleForEach(x => x.Cart.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage(localizer["QuantityMustBePositive"]);
        });
    }
}
```

> **`RuleForEach` + `ChildRules`** — valida cada item da lista. Se algum item tem quantity ≤ 0, retorna 400 com a mensagem i18n.

### 8.4 — Endpoint

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/StoreBasket/StoreBasketEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Basket.Basket.Features.StoreBasket;

public record StoreBasketRequest(ShoppingCart Cart);

public class StoreBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/baskets", async (
            StoreBasketRequest request, ISender sender) =>
        {
            var result = await sender.Send(new StoreBasketCommand(request.Cart));

            return result.ToCreatedResult($"/api/baskets/{request.Cart.UserName}");
        });
    }
}
```

---

## STEP 9 — GetBasket

### 9.1 — Query

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/GetBasket/GetBasketQuery.cs`

```csharp
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Basket.Basket.Features.GetBasket;

public record GetBasketQuery(string UserName)
    : IQuery<Result<ShoppingCart>>;
```

### 9.2 — Handler

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/GetBasket/GetBasketHandler.cs`

```csharp
using Basket.Data;
using Basket.Resources;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Basket.Basket.Features.GetBasket;

public class GetBasketHandler(
    IBasketRepository repository,
    IStringLocalizer<BasketMessages> localizer)
    : IQueryHandler<GetBasketQuery, Result<ShoppingCart>>
{
    public async Task<Result<ShoppingCart>> Handle(
        GetBasketQuery query, CancellationToken cancellationToken)
    {
        var cart = await repository.GetBasketAsync(query.UserName, cancellationToken);

        if (cart is null)
            return Result.Failure<ShoppingCart>(
                Error.NotFound("Basket.NotFound", localizer["BasketNotFound", query.UserName]));

        return Result.Success(cart);
    }
}
```

### 9.3 — Endpoint

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/GetBasket/GetBasketEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Basket.Basket.Features.GetBasket;

public class GetBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/baskets/{userName}", async (
            string userName, ISender sender) =>
        {
            var result = await sender.Send(new GetBasketQuery(userName));

            return result.ToProblemResult();
        });
    }
}
```

---

## STEP 10 — DeleteBasket

### 10.1 — Command

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/DeleteBasket/DeleteBasketCommand.cs`

```csharp
using Shared.Contracts.CQRS;

namespace Basket.Basket.Features.DeleteBasket;

public record DeleteBasketCommand(string UserName) : ICommand;
```

### 10.2 — Handler

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/DeleteBasket/DeleteBasketHandler.cs`

```csharp
using Basket.Data;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Basket.Basket.Features.DeleteBasket;

public class DeleteBasketHandler(IBasketRepository repository)
    : ICommandHandler<DeleteBasketCommand>
{
    public async Task<Result> Handle(
        DeleteBasketCommand command, CancellationToken cancellationToken)
    {
        await repository.DeleteBasketAsync(command.UserName, cancellationToken);

        return Result.Success();
    }
}
```

### 10.3 — Endpoint

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/DeleteBasket/DeleteBasketEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;

namespace Basket.Basket.Features.DeleteBasket;

public class DeleteBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/baskets/{userName}", async (
            string userName, ISender sender) =>
        {
            var result = await sender.Send(new DeleteBasketCommand(userName));

            return result.ToProblemResult();
        });
    }
}
```

---

## STEP 11 — CheckoutBasket

> **Este é o passo mais importante da Fase 2.** É aqui que módulos se comunicam via evento assíncrono.

### 11.1 — Command

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/CheckoutBasket/CheckoutBasketCommand.cs`

```csharp
using Shared.Contracts.CQRS;
using Shared.Messaging.Events;

namespace Basket.Basket.Features.CheckoutBasket;

public record CheckoutBasketCommand(BasketCheckoutIntegrationEvent CheckoutDto)
    : ICommand;
```

> **O Command recebe o `BasketCheckoutIntegrationEvent` diretamente** — ele já tem todos os campos (UserName, endereço, pagamento). Na prática, o endpoint mapeia o request para esse DTO.

### 11.2 — Handler

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/CheckoutBasket/CheckoutBasketHandler.cs`

```csharp
using Basket.Data;
using Basket.Resources;
using MassTransit;
using Microsoft.Extensions.Localization;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;
using Shared.Messaging.Events;

namespace Basket.Basket.Features.CheckoutBasket;

public class CheckoutBasketHandler(
    IBasketRepository repository,
    IPublishEndpoint publishEndpoint,
    IStringLocalizer<BasketMessages> localizer)
    : ICommandHandler<CheckoutBasketCommand>
{
    public async Task<Result> Handle(
        CheckoutBasketCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.GetBasketAsync(
            command.CheckoutDto.UserName, cancellationToken);

        if (cart is null)
            return Result.Failure(
                Error.NotFound("Basket.NotFound",
                    localizer["BasketNotFound", command.CheckoutDto.UserName]));

        var integrationEvent = new BasketCheckoutIntegrationEvent
        {
            UserName = cart.UserName,
            TotalPrice = cart.TotalPrice,
            // Shipping & Payment from checkout DTO
            FirstName = command.CheckoutDto.FirstName,
            LastName = command.CheckoutDto.LastName,
            EmailAddress = command.CheckoutDto.EmailAddress,
            AddressLine = command.CheckoutDto.AddressLine,
            Country = command.CheckoutDto.Country,
            State = command.CheckoutDto.State,
            ZipCode = command.CheckoutDto.ZipCode,
            CardName = command.CheckoutDto.CardName,
            CardNumber = command.CheckoutDto.CardNumber,
            Expiration = command.CheckoutDto.Expiration,
            Cvv = command.CheckoutDto.Cvv,
            PaymentMethod = command.CheckoutDto.PaymentMethod
        };

        // Publish to RabbitMQ → Orders module will consume this
        await publishEndpoint.Publish(integrationEvent, cancellationToken);

        // Clear the basket after checkout
        await repository.DeleteBasketAsync(cart.UserName, cancellationToken);

        return Result.Success();
    }
}
```

> **Fluxo:**
> 1. Busca o carrinho no Redis
> 2. Monta o `BasketCheckoutIntegrationEvent` com dados do carrinho + checkout
> 3. **Publica no RabbitMQ** via `IPublishEndpoint` (MassTransit)
> 4. Limpa o carrinho
>
> **`IPublishEndpoint`** — injetado automaticamente pelo MassTransit. Publica o evento na exchange do RabbitMQ. Na Fase 3, o Orders vai ter um Consumer que consome esse evento e cria o pedido.

### 11.3 — Validator

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/CheckoutBasket/CheckoutBasketValidator.cs`

```csharp
using Basket.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Basket.Basket.Features.CheckoutBasket;

public class CheckoutBasketValidator : AbstractValidator<CheckoutBasketCommand>
{
    public CheckoutBasketValidator(IStringLocalizer<BasketMessages> localizer)
    {
        RuleFor(x => x.CheckoutDto.UserName)
            .NotEmpty().WithMessage(localizer["UserNameRequired"]);
    }
}
```

### 11.4 — Endpoint

**Arquivo:** `src/Modules/Basket/Basket/Basket/Features/CheckoutBasket/CheckoutBasketEndpoint.cs`

```csharp
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Extensions;
using Shared.Messaging.Events;

namespace Basket.Basket.Features.CheckoutBasket;

public class CheckoutBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/baskets/checkout", async (
            BasketCheckoutIntegrationEvent checkoutDto, ISender sender) =>
        {
            var result = await sender.Send(new CheckoutBasketCommand(checkoutDto));

            return result.ToProblemResult();
        });
    }
}
```

---

## STEP 12 — BasketModule.cs

**Por quê:** Encapsula todo o DI do módulo: Redis, Repository, Decorator, MemoryCache.

**Arquivo:** `src/Modules/Basket/Basket/BasketModule.cs`

```csharp
using Basket.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basket;

public static class BasketModule
{
    public static IServiceCollection AddBasketModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Redis as distributed cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });

        // In-memory cache (for Decorator)
        services.AddMemoryCache();

        // Repository + Decorator
        services.AddScoped<IBasketRepository, BasketRepository>();
        services.Decorate<IBasketRepository, CachedBasketRepository>();

        return services;
    }
}
```

> **`services.Decorate<IBasketRepository, CachedBasketRepository>()`** — isso é o Scrutor. Ele:
> 1. Pega o registro existente de `IBasketRepository` → `BasketRepository`
> 2. Envolve com `CachedBasketRepository`
> 3. Quando alguém pede `IBasketRepository`, recebe `CachedBasketRepository` (que internamente tem o `BasketRepository`)
>
> **A ordem importa:** primeiro `AddScoped<IBasketRepository, BasketRepository>()`, depois `Decorate<>()`.

---

## STEP 13 — appsettings.json

**Arquivo:** `src/Bootstrapper/Api/appsettings.json`

**Adicionar** Redis connection string e RabbitMQ config:

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
    "DefaultConnection": "Host=localhost;Port=5432;Database=OrderManagementDb;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "RabbitMQ": {
    "Host": "localhost"
  },
  "MessageBroker": {
    "UserName": "guest",
    "Password": "guest"
  }
}
```

> **Redis:** connection string simples. Sem user/password em dev.
> **RabbitMQ:** `guest/guest` é o default da imagem Docker `rabbitmq:management`. O painel fica em `http://localhost:15672`.

---

## STEP 14 — Program.cs

**Arquivo:** `src/Bootstrapper/Api/Program.cs`

**O que adicionar** (além do que já tem do Catalog):

**Novos usings:**

```csharp
using Basket;
using Shared.Messaging.Extensions;
```

**No bloco de services (antes do `var app = builder.Build()`):**

```csharp
// Adicionar assembly do Basket no MediatR e Carter (junto com Catalog)
var catalogAssembly = typeof(CatalogModule).Assembly;
var basketAssembly = typeof(BasketModule).Assembly;
builder.Services.AddMediatRWithAssemblies(catalogAssembly, basketAssembly);
builder.Services.AddCarterWithAssemblies(catalogAssembly, basketAssembly);

// Basket module (Redis + Repository + Decorator)
builder.Services.AddBasketModule(builder.Configuration);

// MassTransit + RabbitMQ (scan consumers de todos os módulos)
builder.Services.AddMassTransitWithAssemblies(
    builder.Configuration, catalogAssembly, basketAssembly);
```

**Estado final completo do Program.cs (com Catalog + Basket):**

```csharp
using System.Globalization;
using Basket;
using Carter;
using Catalog;
using Catalog.Data;
using Shared.Data.Interceptors;
using Shared.Exceptions.Handler;
using Shared.Extensions;
using Shared.Messaging.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Safety net
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();

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

// CQRS + Validation + Logging pipeline
var catalogAssembly = typeof(CatalogModule).Assembly;
var basketAssembly = typeof(BasketModule).Assembly;
builder.Services.AddMediatRWithAssemblies(catalogAssembly, basketAssembly);

// Carter endpoints
builder.Services.AddCarterWithAssemblies(catalogAssembly, basketAssembly);

// EF Core interceptors
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditableEntityInterceptor>();
builder.Services.AddScoped<DispatchDomainEventsInterceptor>();

// Modules
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddBasketModule(builder.Configuration);

// MassTransit + RabbitMQ
builder.Services.AddMassTransitWithAssemblies(
    builder.Configuration, catalogAssembly, basketAssembly);

var app = builder.Build();

// Pipeline
app.UseExceptionHandler();
app.UseRequestLocalization();
app.UseHttpsRedirection();
app.MapCarter();

// Auto-migrate Catalog (Basket uses Redis — no migrations)
await app.UseMigrationAsync<CatalogDbContext>();

app.Run();
```

> **Basket não tem `UseMigrationAsync`** — não usa EF Core. O Redis não precisa de schema.

---

## STEP 15 — Testar

**Subir as dependências:**

```bash
docker run -d --name redis -p 6379:6379 redis
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management
```

**Rodar a API (F5)** e testar:

```
✅ POST   /api/baskets
   Body: { "cart": { "userName": "john", "items": [{ "productId": "...", "productName": "iPhone", "price": 999.99, "quantity": 2 }] } }
   → 201 Created

✅ POST   /api/baskets (userName vazio)
   → 400 ProblemDetails (validation)

✅ POST   /api/baskets (quantity 0)
   → 400 ProblemDetails (validation)

✅ GET    /api/baskets/john
   → 200 OK com ShoppingCart + TotalPrice calculado

✅ GET    /api/baskets/usuario-inexistente
   → 404 ProblemDetails

✅ DELETE /api/baskets/john
   → 204 No Content

✅ GET    /api/baskets/john (após delete)
   → 404 ProblemDetails

✅ POST   /api/baskets (re-criar carrinho do john)
   → 201 Created

✅ POST   /api/baskets/checkout
   Body: { "userName": "john", "firstName": "John", "lastName": "Doe", ... }
   → 204 No Content
   → Verificar no RabbitMQ Management (localhost:15672) que a mensagem foi publicada

✅ GET    /api/baskets/john (após checkout)
   → 404 (carrinho foi limpo)
```

> **Para verificar o RabbitMQ:** acesse `http://localhost:15672` (guest/guest) → aba Queues. A mensagem `BasketCheckoutIntegrationEvent` deve aparecer (ficará na fila sem consumer até a Fase 3).

---

## STEP 16 — Commit

```bash
git add -A
git commit -m "feat: implement Basket module - Redis, Decorator Pattern, Checkout with MassTransit integration event"
```

---

## 📁 ESTRUTURA FINAL DO BASKET

```
src/Modules/Basket/Basket/
├── BasketModule.cs
├── Basket.csproj
├── Basket/
│   ├── ShoppingCart.cs
│   ├── ShoppingCartItem.cs
│   └── Features/
│       ├── StoreBasket/
│       │   ├── StoreBasketCommand.cs
│       │   ├── StoreBasketHandler.cs
│       │   ├── StoreBasketValidator.cs
│       │   └── StoreBasketEndpoint.cs
│       ├── GetBasket/
│       │   ├── GetBasketQuery.cs
│       │   ├── GetBasketHandler.cs
│       │   └── GetBasketEndpoint.cs
│       ├── DeleteBasket/
│       │   ├── DeleteBasketCommand.cs
│       │   ├── DeleteBasketHandler.cs
│       │   └── DeleteBasketEndpoint.cs
│       └── CheckoutBasket/
│           ├── CheckoutBasketCommand.cs
│           ├── CheckoutBasketHandler.cs
│           ├── CheckoutBasketValidator.cs
│           └── CheckoutBasketEndpoint.cs
├── Data/
│   ├── IBasketRepository.cs
│   ├── BasketRepository.cs
│   └── CachedBasketRepository.cs
└── Resources/
    ├── BasketMessages.cs                (já existia — trocou pra public)
    ├── BasketMessages.resx              (já existia)
    ├── BasketMessages.pt-BR.resx        (já existia)
    └── BasketMessages.es.resx           (já existia)
```

---

## 🧠 COLA DE PADRÕES FASE 2 (decorar)

| Padrão | Implementação | Por que |
|---|---|---|
| **Repository** | `IBasketRepository` → `BasketRepository` | Abstrai o Redis |
| **Decorator** | `CachedBasketRepository` envolve `BasketRepository` | Memory cache na frente do Redis |
| **Scrutor** | `services.Decorate<IBasketRepository, CachedBasketRepository>()` | Registro do decorator no DI |
| **IDistributedCache** | `AddStackExchangeRedisCache` | Abstração .NET com Redis como provider |
| **IPublishEndpoint** | `publishEndpoint.Publish(event)` | Publica no RabbitMQ via MassTransit |
| **Integration Event** | `BasketCheckoutIntegrationEvent` | Comunicação Basket → Orders (assíncrona) |

| Diferença vs Catalog | Basket |
|---|---|
| Storage | Redis (não EF Core) |
| Migration | Nenhuma |
| Entidade | Não herda Entity<T> |
| Novo padrão | Decorator, Repository |
| Comunicação | Publica IntegrationEvent |

---

## 🔄 SPEEDRUN RESET

```bash
git stash -u
git checkout core-ready
git stash drop
```
