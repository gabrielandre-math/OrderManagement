# 🌐 Guia Completo de Internacionalização (i18n) — OrderManagementApi

## Índice

1. [O que é i18n?](#1-o-que-é-i18n)
2. [Como o i18n funciona no ASP.NET Core](#2-como-o-i18n-funciona-no-aspnet-core)
3. [Arquitetura i18n no nosso projeto](#3-arquitetura-i18n-no-nosso-projeto)
4. [O que já foi implementado (Passos 1-5)](#4-o-que-já-foi-implementado-passos-1-5)
5. [O que falta implementar (Passos 6-7)](#5-o-que-falta-implementar-passos-6-7)
6. [Catálogo completo de chaves .resx](#6-catálogo-completo-de-chaves-resx)
7. [Regras e boas práticas](#7-regras-e-boas-práticas)
8. [Como testar](#8-como-testar)
9. [Como adicionar um novo idioma](#9-como-adicionar-um-novo-idioma)
10. [FAQ e troubleshooting](#10-faq-e-troubleshooting)

---

## 1. O que é i18n?

**i18n** é a abreviação de "internationalization" (i + 18 letras + n). É o processo de projetar
software para que ele possa ser **adaptado a diferentes idiomas e regiões** sem alterar código.

### Conceitos fundamentais

| Conceito | Significado | Exemplo |
|---|---|---|
| **i18n** (Internationalization) | Preparar o código para suportar múltiplos idiomas | Usar `localizer["Chave"]` em vez de `"texto fixo"` |
| **L10n** (Localization) | Traduzir de fato para um idioma específico | Criar `SharedMessages.pt-BR.resx` com as traduções |
| **Culture** | Idioma + região | `pt-BR` = Português do Brasil |
| **UICulture** | Culture usada para buscar recursos (`.resx`) | Determina qual `.resx` será carregado |
| **Fallback** | Idioma de reserva quando a tradução não existe | `pt-BR` → `pt` → `en-US` (padrão) |
| **Resource file (.resx)** | Arquivo XML com pares chave=valor por idioma | `SharedMessages.pt-BR.resx` |
| **Marker class** | Classe vazia que aponta para os `.resx` | `public class SharedMessages { }` |

### Por que usar i18n em uma API?

Mesmo APIs (sem interface visual) se beneficiam de i18n:
- **Mensagens de erro** traduzidas para o cliente (`ProblemDetails`)
- **Mensagens de validação** do FluentValidation no idioma do usuário
- **Respostas de sucesso** localizadas
- **Documentação da API** (Swagger) pode ser localizada
- Facilita integração com **frontends multilíngue**

---

## 2. Como o i18n funciona no ASP.NET Core

### O pipeline completo (passo a passo de um request)

```
Cliente envia request
    │
    │  Header: Accept-Language: pt-BR
    │
    ▼
┌─────────────────────────────────────────┐
│  UseRequestLocalization() middleware    │
│                                         │
│  1. Lê o Accept-Language do header      │
│  2. Verifica se pt-BR está em           │
│     SupportedCultures                   │
│  3. Define:                             │
│     CultureInfo.CurrentCulture = pt-BR  │
│     CultureInfo.CurrentUICulture = pt-BR│
└─────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────┐
│  Seu código (handler/validator/etc)     │
│                                         │
│  localizer["ProductNotFound"]           │
│     │                                   │
│     ▼                                   │
│  IStringLocalizer<CatalogMessages>      │
│     │                                   │
│     ▼                                   │
│  Verifica CurrentUICulture = pt-BR      │
│     │                                   │
│     ▼                                   │
│  Busca CatalogMessages.pt-BR.resx      │
│     │                                   │
│     ▼                                   │
│  Chave "ProductNotFound" encontrada?    │
│     SIM → retorna valor em pt-BR        │
│     NÃO → fallback para .resx neutro   │
└─────────────────────────────────────────┘
    │
    ▼
Cliente recebe resposta em pt-BR
```

### Os 3 Request Culture Providers (ordem de prioridade)

O middleware `UseRequestLocalization()` detecta o idioma nesta ordem:

| Prioridade | Provider | Como funciona | Exemplo |
|---|---|---|---|
| 1️⃣ | **QueryStringRequestCultureProvider** | Parâmetro na URL | `?culture=pt-BR&ui-culture=pt-BR` |
| 2️⃣ | **CookieRequestCultureProvider** | Cookie no browser | `.AspNetCore.Culture=c=pt-BR\|uic=pt-BR` |
| 3️⃣ | **AcceptLanguageHeaderRequestCultureProvider** | Header HTTP | `Accept-Language: pt-BR` |

Se nenhum provider encontrar um idioma suportado, usa o `DefaultRequestCulture` (no nosso caso: `en-US`).

### `IStringLocalizer<T>` — como resolve os arquivos

O `IStringLocalizer<T>` usa o tipo `T` para calcular o caminho do `.resx`:

```
Tipo T = Shared.Resources.SharedMessages
                │              │
                │              └── Nome do arquivo: SharedMessages
                │
                └── Namespace raiz do assembly: Shared
                    ResourcesPath configurado: "Resources"

Caminho calculado: Resources/SharedMessages.{culture}.resx
```

**É por isso que precisamos das marker classes** — sem elas, o localizer não sabe qual `.resx` carregar.

### `Culture` vs `UICulture` — qual a diferença?

| Propriedade | Afeta | Exemplo |
|---|---|---|
| `CultureInfo.CurrentCulture` | Formatação de datas, números, moeda | `1.234,56` (pt-BR) vs `1,234.56` (en-US) |
| `CultureInfo.CurrentUICulture` | Qual `.resx` é carregado | `SharedMessages.pt-BR.resx` |

No nosso caso configuramos ambos iguais, mas poderiam ser diferentes (ex: formatar números em pt-BR mas mostrar mensagens em inglês).

---

## 3. Arquitetura i18n no nosso projeto

### Estrutura de arquivos

```
OrderManagementApi/
│
├── src/Bootstrapper/Api/
│   └── Program.cs                          ← Middleware de localização configurado
│
├── src/Shared/Shared/
│   ├── Shared.csproj                       ← Microsoft.Extensions.Localization 8.0.26
│   ├── Resources/
│   │   ├── SharedMessages.cs               ← Marker class
│   │   ├── SharedMessages.resx             ← Inglês (padrão/fallback)
│   │   ├── SharedMessages.pt-BR.resx       ← Português BR
│   │   └── SharedMessages.es.resx          ← Espanhol
│   ├── Exceptions/Handler/
│   │   └── CustomExceptionHandler.cs       ← Já usa IStringLocalizer<SharedMessages>
│   └── Behaviors/
│       └── ValidationBehavior.cs           ← Lança ValidationException (mensagens vêm dos validators)
│
├── src/Modules/Catalog/Catalog/
│   └── Resources/
│       ├── CatalogMessages.cs              ← Marker class
│       ├── CatalogMessages.resx            ← Inglês
│       ├── CatalogMessages.pt-BR.resx      ← Português BR
│       └── CatalogMessages.es.resx         ← Espanhol
│
├── src/Modules/Orders/Orders/
│   └── Resources/
│       ├── OrdersMessages.cs               ← Marker class
│       ├── OrdersMessages.resx             ← Inglês
│       ├── OrdersMessages.pt-BR.resx       ← Português BR
│       └── OrdersMessages.es.resx          ← Espanhol
│
└── src/Modules/Basket/Basket/
    └── Resources/
        ├── BasketMessages.cs               ← Marker class
        ├── BasketMessages.resx             ← Inglês
        ├── BasketMessages.pt-BR.resx       ← Português BR
        └── BasketMessages.es.resx          ← Espanhol
```

### Fluxo completo de uma exceção localizada

```
Handler lança NotFoundException(localizer["ProductNotFound", id])
    │
    ▼
CustomExceptionHandler captura a exceção
    │
    ├── exception.Message = "Produto com ID '123' não foi encontrado." (já traduzido)
    │
    ├── details.Title = "NotFoundException" (nome do tipo C#)
    │
    ├── localizer["NotFoundException"] → busca no SharedMessages.pt-BR.resx
    │   → retorna "Não Encontrado"
    │
    ▼
ProblemDetails retornado:
{
    "title": "Não Encontrado",
    "detail": "Produto com ID '123' não foi encontrado.",
    "status": 404,
    "instance": "/api/products/123",
    "traceId": "00-abc..."
}
```

---

## 4. O que já foi implementado (Passos 1-5)

### ✅ Passo 1 — Pacote NuGet

**Arquivo:** `src/Shared/Shared/Shared.csproj`

```xml
<PackageReference Include="Microsoft.Extensions.Localization" Version="8.0.26" />
```

**O que faz:** Disponibiliza `IStringLocalizer<T>` e `IStringLocalizerFactory` para class libraries.
O projeto `Api.csproj` não precisa desse pacote porque o `Microsoft.AspNetCore.App` FrameworkReference
já inclui tudo.

---

### ✅ Passo 2 — Middleware no Program.cs

**Arquivo:** `src/Bootstrapper/Api/Program.cs`

```csharp
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// i18n: Enable request localization middleware (must be before endpoints)
app.UseRequestLocalization();

app.UseHttpsRedirection();

app.Run();
```

**Explicação linha a linha:**

| Linha | O que faz |
|---|---|
| `AddLocalization(options => options.ResourcesPath = "Resources")` | Registra os serviços de localização no DI. `ResourcesPath` diz onde ficam os `.resx` dentro de cada projeto |
| `new CultureInfo("en-US")` / `"pt-BR"` / `"es"` | Define os idiomas que a aplicação aceita |
| `DefaultRequestCulture = new RequestCulture("en-US")` | Se o cliente não especificar idioma, usa inglês americano |
| `SupportedCultures` | Culturas para formatação (datas, números) |
| `SupportedUICultures` | Culturas para busca de `.resx` |
| `ApplyCurrentCultureToResponseHeaders = true` | Retorna `Content-Language: pt-BR` no header da resposta |
| `app.UseRequestLocalization()` | **Posição importa!** Deve vir ANTES de endpoints/controllers. Lê o idioma do request e define `CurrentCulture`/`CurrentUICulture` na thread |

---

### ✅ Passo 3 — Arquivos .resx (12 arquivos)

Foram criados 12 arquivos `.resx` (4 módulos × 3 idiomas).

**Estrutura de um .resx:**
```xml
<data name="ChaveSemântica" xml:space="preserve">
    <value>Texto traduzido com {0} placeholders</value>
    <comment>Comentário opcional para tradutores</comment>
</data>
```

**Convenção de nomes:**
- `NomeBase.resx` → cultura neutra (inglês, é o fallback final)
- `NomeBase.pt-BR.resx` → português do Brasil
- `NomeBase.es.resx` → espanhol (genérico, cobre es-AR, es-MX, etc.)

---

### ✅ Passo 4 — Marker Classes (4 arquivos)

**Arquivos criados:**

| Arquivo | Namespace | Aponta para |
|---|---|---|
| `src/Shared/Shared/Resources/SharedMessages.cs` | `Shared.Resources` | `SharedMessages.resx` |
| `src/Modules/Catalog/Catalog/Resources/CatalogMessages.cs` | `Catalog.Resources` | `CatalogMessages.resx` |
| `src/Modules/Orders/Orders/Resources/OrdersMessages.cs` | `Orders.Resources` | `OrdersMessages.resx` |
| `src/Modules/Basket/Basket/Resources/BasketMessages.cs` | `Basket.Resources` | `BasketMessages.resx` |

**Por que o namespace importa?**

O `IStringLocalizer` calcula o caminho do `.resx` assim:

```
Assembly root namespace:  Shared
Marker class full name:   Shared.Resources.SharedMessages
ResourcesPath:            Resources

Cálculo:
  Full name sem root namespace:  Resources.SharedMessages
  Caminho no disco:              Resources/SharedMessages.resx  ✓ MATCH!
```

Se o namespace estivesse errado (ex: `Shared.Exceptions.SharedMessages`), o localizer procuraria
`Exceptions/SharedMessages.resx` e **não encontraria nada** (retornaria a própria chave como fallback
sem erro, dificultando debug).

---

### ✅ Passo 5 — CustomExceptionHandler localizado

**Arquivo:** `src/Shared/Shared/Exceptions/Handler/CustomExceptionHandler.cs`

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;     // ← Adicionado
using Shared.Resources;                      // ← Adicionado

namespace Shared.Exceptions.Handler;

public class CustomExceptionHandler
    (ILogger<CustomExceptionHandler> logger,
     IStringLocalizer<SharedMessages> localizer)   // ← Adicionado
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Log SEMPRE em inglês (não localizar logs!)
        logger.LogError(
            "Error Message: {excpetionMessage}, Time of occurrence: {time}",
            exception.Message, DateTime.UtcNow);

        (string Detail, string Title, int StatusCode) details = exception switch
        {
            InternalServerException => (
                exception.Message,
                exception.GetType().Name,      // "InternalServerException"
                context.Response.StatusCode = StatusCodes.Status500InternalServerError),
            ValidationException => (
                exception.Message,
                exception.GetType().Name,      // "ValidationException"
                context.Response.StatusCode = StatusCodes.Status400BadRequest),
            BadRequestException => (
                exception.Message,
                exception.GetType().Name,      // "BadRequestException"
                context.Response.StatusCode = StatusCodes.Status400BadRequest),
            NotFoundException => (
                exception.Message,
                exception.GetType().Name,      // "NotFoundException"
                context.Response.StatusCode = StatusCodes.Status404NotFound),
            _ => (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError)
        };

        var problemDetails = new ProblemDetails
        {
            Title = localizer[details.Title],  // ← LOCALIZADO!
            //      ↑ busca "NotFoundException" no SharedMessages.{culture}.resx
            //      pt-BR → "Não Encontrado"
            //      es    → "No Encontrado"
            //      en-US → "Not Found"
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = context.Request.Path
        };

        problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);
        }

        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        return true;
    }
}
```

**Impacto:** O `Title` do ProblemDetails agora é traduzido automaticamente. O `Detail` (que vem
de `exception.Message`) será traduzido quando os handlers passarem mensagens localizadas ao lançar exceções
(Passo 7).

---

## 5. O que falta implementar (Passos 6-7)

### 🔲 Passo 6 — Localizar FluentValidation nos Validators

**Quando aplicar:** Ao criar qualquer `AbstractValidator<T>` nos módulos.

**Onde:** Dentro de cada módulo, nos validators de commands.

**Padrão completo:**

```csharp
// src/Modules/Catalog/Catalog/Features/CreateProduct/CreateProductCommandValidator.cs

using FluentValidation;
using Microsoft.Extensions.Localization;
using Catalog.Resources;                    // ← Marker class

namespace Catalog.Features.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    // O IStringLocalizer é injetado via DI no construtor
    // O FluentValidation suporta DI nativamente nos validators
    public CreateProductCommandValidator(IStringLocalizer<CatalogMessages> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(localizer["ProductNameRequired"]);
            //          ↑
            // en-US: "Product name is required."
            // pt-BR: "O nome do produto é obrigatório."
            // es:    "El nombre del producto es obligatorio."

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage(localizer["ProductPriceMustBePositive"]);
    }
}
```

**Como funciona por dentro:**

```
1. Request chega com Accept-Language: pt-BR
2. Middleware define CurrentUICulture = pt-BR
3. MediatR despacha o Command
4. ValidationBehavior executa ANTES do handler
5. FluentValidation cria instância do Validator via DI
6. DI injeta IStringLocalizer<CatalogMessages> no construtor
7. localizer["ProductNameRequired"] busca em CatalogMessages.pt-BR.resx
8. Se validação falha → ValidationException com mensagem em pt-BR
9. CustomExceptionHandler captura e retorna ProblemDetails localizado
```

**Aplicar o mesmo padrão para:**

| Módulo | Localizer a injetar | Chaves disponíveis |
|---|---|---|
| **Catalog** | `IStringLocalizer<CatalogMessages>` | `ProductNameRequired`, `ProductPriceMustBePositive` |
| **Orders** | `IStringLocalizer<OrdersMessages>` | `CustomerIdRequired`, `OrderItemsRequired` |
| **Basket** | `IStringLocalizer<BasketMessages>` | `UserNameRequired`, `QuantityMustBePositive` |

> **Dica:** O FluentValidation já vem com traduções internas para mensagens padrão (como "must not be
> empty") em ~30 idiomas incluindo pt-BR. Elas funcionam automaticamente quando o `CurrentUICulture`
> está correto. O `.WithMessage()` só é necessário para **substituir** a mensagem padrão por uma
> customizada.

---

### 🔲 Passo 7 — Localizar mensagens nos Handlers CQRS

**Quando aplicar:** Ao criar qualquer `ICommandHandler` ou `IQueryHandler` que lance exceções.

**Onde:** Dentro de cada módulo, nos handlers de commands/queries.

**Padrão completo:**

```csharp
// src/Modules/Catalog/Catalog/Features/GetProduct/GetProductHandler.cs

using Microsoft.Extensions.Localization;
using Catalog.Resources;
using Shared.Contracts.CQRS;
using Shared.Exceptions;

namespace Catalog.Features.GetProduct;

public class GetProductHandler(
    IStringLocalizer<CatalogMessages> localizer,    // ← Injetar
    CatalogDbContext db)
    : IQueryHandler<GetProductQuery, ProductDto>
{
    public async Task<ProductDto> Handle(
        GetProductQuery query, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync(query.Id, cancellationToken);

        if (product is null)
        {
            // string.Format substitui {0} pelo valor real
            throw new NotFoundException(
                string.Format(localizer["ProductNotFound"], query.Id));
            //                         ↑
            // en-US: Product with ID "abc-123" was not found.
            // pt-BR: Produto com ID "abc-123" não foi encontrado.
            // es:    El producto con ID "abc-123" no fue encontrado.
        }

        return product.ToDto();
    }
}
```

**Outra opção — mensagens com múltiplos placeholders:**

```csharp
// Chave no .resx: InvalidOrderStatus = "Invalid order status transition from \"{0}\" to \"{1}\"."
throw new BadRequestException(
    string.Format(localizer["InvalidOrderStatus"], currentStatus, newStatus));
// pt-BR: Transição de status de pedido inválida de "Pending" para "Cancelled".
```

**Mensagens genéricas do Shared:**

```csharp
// Se precisar de mensagens compartilhadas em qualquer módulo:
using Shared.Resources;  // ← SharedMessages marker class

public class SomeHandler(
    IStringLocalizer<SharedMessages> sharedLocalizer,
    IStringLocalizer<OrdersMessages> ordersLocalizer)
{
    // Use sharedLocalizer para mensagens genéricas (EntityNotFound, BadRequest, etc.)
    // Use ordersLocalizer para mensagens específicas do módulo
}
```

**Aplicar o mesmo padrão para:**

| Módulo | Localizer | Chaves de exceção disponíveis |
|---|---|---|
| **Catalog** | `IStringLocalizer<CatalogMessages>` | `ProductNotFound` |
| **Orders** | `IStringLocalizer<OrdersMessages>` | `OrderNotFound`, `InvalidOrderStatus` |
| **Basket** | `IStringLocalizer<BasketMessages>` | `BasketNotFound`, `BasketItemNotFound` |
| **Shared** | `IStringLocalizer<SharedMessages>` | `EntityNotFound`, `BadRequest`, `InternalServerError` |

---

## 6. Catálogo completo de chaves .resx

### SharedMessages (Shared)

| Chave | en-US | pt-BR | es | Uso |
|---|---|---|---|---|
| `EntityNotFound` | Entity "{0}" ({1}) was not found. | Entidade "{0}" ({1}) não foi encontrada. | La entidad "{0}" ({1}) no fue encontrada. | Exceções genéricas |
| `BadRequest` | The request is invalid. | A requisição é inválida. | La solicitud no es válida. | Exceções genéricas |
| `InternalServerError` | An internal server error has occurred. | Ocorreu um erro interno no servidor. | Se ha producido un error interno en el servidor. | Exceções genéricas |
| `ValidationFailed` | One or more validation errors occurred. | Um ou mais erros de validação ocorreram. | Se produjeron uno o más errores de validación. | Pipeline de validação |
| `UnexpectedError` | An unexpected error has occurred. | Ocorreu um erro inesperado. | Se ha producido un error inesperado. | Fallback genérico |
| `NotFoundException` | Not Found | Não Encontrado | No Encontrado | ProblemDetails Title |
| `BadRequestException` | Bad Request | Requisição Inválida | Solicitud Inválida | ProblemDetails Title |
| `InternalServerException` | Internal Server Error | Erro Interno do Servidor | Error Interno del Servidor | ProblemDetails Title |
| `ValidationException` | Validation Error | Erro de Validação | Error de Validación | ProblemDetails Title |

### CatalogMessages (Catalog)

| Chave | en-US | pt-BR | es |
|---|---|---|---|
| `ProductNotFound` | Product with ID "{0}" was not found. | Produto com ID "{0}" não foi encontrado. | El producto con ID "{0}" no fue encontrado. |
| `ProductNameRequired` | Product name is required. | O nome do produto é obrigatório. | El nombre del producto es obligatorio. |
| `ProductPriceMustBePositive` | Product price must be greater than zero. | O preço do produto deve ser maior que zero. | El precio del producto debe ser mayor que cero. |
| `ProductCreated` | Product "{0}" was created successfully. | Produto "{0}" criado com sucesso. | Producto "{0}" creado con éxito. |
| `ProductUpdated` | Product "{0}" was updated successfully. | Produto "{0}" atualizado com sucesso. | Producto "{0}" actualizado con éxito. |
| `ProductDeleted` | Product "{0}" was deleted successfully. | Produto "{0}" excluído com sucesso. | Producto "{0}" eliminado con éxito. |

### OrdersMessages (Orders)

| Chave | en-US | pt-BR | es |
|---|---|---|---|
| `OrderNotFound` | Order with ID "{0}" was not found. | Pedido com ID "{0}" não foi encontrado. | El pedido con ID "{0}" no fue encontrado. |
| `OrderCreated` | Order "{0}" was created successfully. | Pedido "{0}" criado com sucesso. | Pedido "{0}" creado con éxito. |
| `OrderCancelled` | Order "{0}" was cancelled successfully. | Pedido "{0}" cancelado com sucesso. | Pedido "{0}" cancelado con éxito. |
| `CustomerIdRequired` | Customer ID is required. | O ID do cliente é obrigatório. | El ID del cliente es obligatorio. |
| `OrderItemsRequired` | At least one order item is required. | Pelo menos um item de pedido é obrigatório. | Se requiere al menos un artículo en el pedido. |
| `InvalidOrderStatus` | Invalid order status transition from "{0}" to "{1}". | Transição de status de pedido inválida de "{0}" para "{1}". | Transición de estado de pedido inválida de "{0}" a "{1}". |

### BasketMessages (Basket)

| Chave | en-US | pt-BR | es |
|---|---|---|---|
| `BasketNotFound` | Basket for user "{0}" was not found. | Cesta do usuário "{0}" não foi encontrada. | La cesta del usuario "{0}" no fue encontrada. |
| `BasketItemNotFound` | Basket item with product ID "{0}" was not found. | Item da cesta com ID de produto "{0}" não foi encontrado. | El artículo de la cesta con ID de producto "{0}" no fue encontrado. |
| `UserNameRequired` | User name is required. | O nome do usuário é obrigatório. | El nombre de usuario es obligatorio. |
| `QuantityMustBePositive` | Item quantity must be greater than zero. | A quantidade do item deve ser maior que zero. | La cantidad del artículo debe ser mayor que cero. |
| `BasketCheckedOut` | Basket for user "{0}" was checked out successfully. | Cesta do usuário "{0}" finalizada com sucesso. | La cesta del usuario "{0}" fue finalizada con éxito. |
| `BasketCleared` | Basket for user "{0}" was cleared successfully. | Cesta do usuário "{0}" limpa com sucesso. | La cesta del usuario "{0}" fue vaciada con éxito. |

---

## 7. Regras e boas práticas

### ✅ FAZER

| Regra | Motivo |
|---|---|
| Localizar tudo que vai para o **cliente** (ProblemDetails, validation errors, success messages) | É o que o usuário final vê |
| Usar **chaves semânticas** (`ProductNotFound`, não `Mensagem1`) | Legibilidade e manutenção |
| Manter o `.resx` neutro (sem cultura) como **inglês** | É o fallback universal |
| Usar `{0}`, `{1}` para placeholders com `string.Format()` | Permite reordenar palavras por idioma |
| Testar com culturas diferentes durante desenvolvimento | Pega problemas de layout/tamanho |
| Cada módulo tem seus próprios `.resx` | Modularidade — cada módulo é independente |

### ❌ NÃO FAZER

| Anti-padrão | Problema |
|---|---|
| **Não localizar logs** (`ILogger`) | Logs devem ser em inglês fixo para facilitar debugging e buscas |
| **Não localizar nomes de propriedades** em JSON | `{"productId": ...}` não muda por idioma |
| **Não concatenar strings traduzidas** | `localizer["Hello"] + " " + name` quebra em idiomas RTL e com gramáticas diferentes |
| **Não colocar HTML nos .resx** | Responsabilidade do frontend |
| **Não usar o mesmo .resx para todos os módulos** | Viola a modularidade e cria acoplamento |

### ⚠️ Cuidado com o LoggingBehavior

O `LoggingBehavior.cs` que já existe no projeto loga nomes de tipos (`typeof(TRequest).Name`),
**não mensagens de usuário**. Está correto — **não altere para usar localizer**.

```csharp
// ✅ CORRETO — log técnico em inglês
logger.LogInformation("[START] Handle request={Request}", typeof(TRequest).Name);

// ❌ ERRADO — nunca faça isso
logger.LogInformation(localizer["HandlingRequest"], typeof(TRequest).Name);
```

---

## 8. Como testar

### Via cURL / Postman / HTTP client

```bash
# Teste em português
curl -H "Accept-Language: pt-BR" https://localhost:5001/api/products/id-inexistente

# Teste em espanhol
curl -H "Accept-Language: es" https://localhost:5001/api/products/id-inexistente

# Teste em inglês (padrão)
curl https://localhost:5001/api/products/id-inexistente

# Teste via query string (prioridade máxima — sobrescreve o header)
curl https://localhost:5001/api/products/id-inexistente?culture=pt-BR&ui-culture=pt-BR
```

### Respostas esperadas

**Accept-Language: en-US** (ou sem header):
```json
{
    "title": "Not Found",
    "detail": "Product with ID \"abc-123\" was not found.",
    "status": 404,
    "instance": "/api/products/abc-123"
}
```

**Accept-Language: pt-BR**:
```json
{
    "title": "Não Encontrado",
    "detail": "Produto com ID \"abc-123\" não foi encontrado.",
    "status": 404,
    "instance": "/api/products/abc-123"
}
```

**Accept-Language: es**:
```json
{
    "title": "No Encontrado",
    "detail": "El producto con ID \"abc-123\" no fue encontrado.",
    "status": 404,
    "instance": "/api/products/abc-123"
}
```

### Via .http file no Visual Studio

```http
### Teste pt-BR
GET https://localhost:5001/api/products/abc-123
Accept-Language: pt-BR

### Teste es
GET https://localhost:5001/api/products/abc-123
Accept-Language: es
```

### Verificar se o Content-Language está na resposta

Como configuramos `ApplyCurrentCultureToResponseHeaders = true`, a resposta terá:
```
Content-Language: pt-BR
```

---

## 9. Como adicionar um novo idioma

Exemplo: adicionar **francês (fr)**.

### Passo 9.1 — Registrar a cultura no Program.cs

```csharp
var supportedCultures = new[]
{
    new CultureInfo("en-US"),
    new CultureInfo("pt-BR"),
    new CultureInfo("es"),
    new CultureInfo("fr")       // ← Adicionar
};
```

### Passo 9.2 — Criar os arquivos .resx

Copiar os `.resx` neutros e renomear com `.fr.resx`:

```
SharedMessages.resx     → copiar para → SharedMessages.fr.resx
CatalogMessages.resx    → copiar para → CatalogMessages.fr.resx
OrdersMessages.resx     → copiar para → OrdersMessages.fr.resx
BasketMessages.resx     → copiar para → BasketMessages.fr.resx
```

### Passo 9.3 — Traduzir os valores

Editar cada `.fr.resx` e substituir os `<value>` pelo francês:

```xml
<data name="ProductNotFound" xml:space="preserve">
    <value>Le produit avec l'ID "{0}" n'a pas été trouvé.</value>
</data>
```

### Passo 9.4 — Pronto!

Nenhuma outra mudança no código. O middleware detecta `Accept-Language: fr` automaticamente
e o `IStringLocalizer` carrega os `.fr.resx`.

---

## 10. FAQ e troubleshooting

### "O localizer retorna a própria chave em vez da tradução"

**Causa mais comum:** O namespace da marker class não bate com o caminho do `.resx`.

**Como verificar:**
1. A marker class `SharedMessages` está em `Shared.Resources` ?
2. O arquivo `.resx` está em `Resources/SharedMessages.resx` ?
3. O `ResourcesPath` no `Program.cs` é `"Resources"` ?

**Debug rápido:** O `IStringLocalizer` tem uma propriedade `ResourceNotFound`:
```csharp
var value = localizer["MinhaChave"];
if (value.ResourceNotFound)
    logger.LogWarning("Chave '{Name}' não encontrada! Caminho buscado: {SearchedLocation}",
        value.Name, value.SearchedLocation);
```

### "FluentValidation não traduz as mensagens padrão"

As mensagens padrão do FluentValidation (ex: "'Name' must not be empty") são traduzidas
automaticamente quando `CurrentUICulture` está correto. Verifique:
1. O middleware `UseRequestLocalization()` está **antes** dos endpoints?
2. O header `Accept-Language` está sendo enviado?

### "Preciso traduzir os nomes dos campos no FluentValidation?"

Por padrão, o FluentValidation usa o nome da propriedade C# (ex: `'Name' must not be empty`).
Para traduzir o nome do campo:
```csharp
RuleFor(x => x.Name)
    .NotEmpty()
    .WithName(localizer["ProductNameField"]);
// Resultado: "O nome do produto não pode ser vazio." (em vez de "'Name' must not be empty")
```

### "Posso usar IStringLocalizer sem a marker class?"

Sim, existe `IStringLocalizer` (sem generic). Mas aí você precisa resolver manualmente o assembly
e o caminho. A marker class é a abordagem recomendada e mais limpa.

### "O que acontece se eu esquecer uma chave no .resx de algum idioma?"

O `IStringLocalizer` faz fallback:
1. Procura em `CatalogMessages.pt-BR.resx`
2. Se não encontrar → procura em `CatalogMessages.pt.resx` (cultura pai)
3. Se não encontrar → procura em `CatalogMessages.resx` (cultura neutra/inglês)
4. Se não encontrar → retorna a própria chave como string (`"ProductNotFound"`)

O sistema **nunca quebra** por falta de tradução — apenas mostra o fallback.

---

## Checklist final

- [x] Passo 1 — `Microsoft.Extensions.Localization` 8.0.26 no `Shared.csproj`
- [x] Passo 2 — `AddLocalization()` + `UseRequestLocalization()` no `Program.cs`
- [x] Passo 3 — 12 arquivos `.resx` (4 módulos × 3 idiomas)
- [x] Passo 4 — 4 marker classes (`SharedMessages`, `CatalogMessages`, `OrdersMessages`, `BasketMessages`)
- [x] Passo 5 — `CustomExceptionHandler` usando `IStringLocalizer<SharedMessages>`
- [ ] Passo 6 — Injetar `IStringLocalizer<XxxMessages>` nos validators FluentValidation
- [ ] Passo 7 — Injetar `IStringLocalizer<XxxMessages>` nos handlers CQRS
