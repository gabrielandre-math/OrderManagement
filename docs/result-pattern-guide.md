# 🎯 Result Pattern + i18n — Guia Completo para OrderManagementApi

## Índice

1. [O que é o Result Pattern?](#1-o-que-é-o-result-pattern)
2. [Por que usar? Problema atual com exceções](#2-por-que-usar-problema-atual-com-exceções)
3. [Arquitetura proposta](#3-arquitetura-proposta)
4. [Implementação passo a passo](#4-implementação-passo-a-passo)
5. [Integração com i18n](#5-integração-com-i18n)
6. [Impacto em cada camada do projeto](#6-impacto-em-cada-camada-do-projeto)
7. [Antes vs Depois — comparação completa](#7-antes-vs-depois--comparação-completa)
8. [Regras e boas práticas](#8-regras-e-boas-práticas)
9. [FAQ](#9-faq)

---

## 1. O que é o Result Pattern?

O **Result Pattern** é uma técnica onde métodos retornam um objeto `Result<T>` que encapsula
**sucesso ou falha de forma explícita**, em vez de lançar exceções para comunicar erros de negócio.

### Analogia simples

```
❌ Sem Result Pattern (exceções):
   "Vou tentar abrir esta porta. Se estiver trancada... EXPLODE!"
   throw new DoorLockedException("Porta trancada")

✅ Com Result Pattern:
   "Vou tentar abrir esta porta. Resultado: não consegui, estava trancada."
   return Result.Failure("Porta trancada")
```

### Princípio fundamental

> **Exceções são para situações excepcionais** (banco caiu, rede falhou, null reference).
> **Erros de negócio são previsíveis** (produto não existe, validação falhou, estoque insuficiente)
> e devem ser tratados como **fluxo normal**, não como exceções.

### Conceitos-chave

| Conceito | Significado |
|---|---|
| `Result<T>` | Objeto que contém sucesso (`T Value`) ou falha (`Error`) |
| `Result` (sem T) | Para operações que não retornam valor (void) |
| `Error` | Objeto tipado que descreve o erro (código, mensagem, tipo) |
| **Railway Programming** | Metáfora: o código segue "trilhos" — trilho do sucesso ou trilho do erro |
| **Explicitness** | A assinatura do método deixa CLARO que pode falhar |

---

## 2. Por que usar? Problema atual com exceções

### Como funciona HOJE no projeto

```
Handler                     ValidationBehavior              CustomExceptionHandler
  │                              │                                │
  │  throw NotFoundException()   │                                │
  │─────────────── BOOM! ───────────────────────────────────────►│
  │                              │                                │ catch + ProblemDetails
  │                              │  throw ValidationException()   │
  │                              │──────── BOOM! ────────────────►│
  │                              │                                │ catch + ProblemDetails
```

**Problemas dessa abordagem:**

| # | Problema | Impacto |
|---|---|---|
| 1 | **Exceções são caras** em performance | Stack trace + unwinding custam ~1000x mais que um `return` |
| 2 | **Fluxo implícito** — quem lê o handler não sabe o que pode falhar | Precisa procurar todos os `throw` dentro do método e das dependências |
| 3 | **Controle perdido** — exceção "voa" até encontrar um catch | Pode pular camadas sem que ninguém saiba |
| 4 | **Difícil de testar** — precisa de `Assert.ThrowsAsync<T>()` | Menos natural que `Assert.False(result.IsSuccess)` |
| 5 | **Mistura exceções técnicas com erros de negócio** | `NotFoundException` (negócio) usa o mesmo mecanismo que `NullReferenceException` (bug) |

### Como ficaria COM Result Pattern

```
Handler                        Endpoint (Carter)
  │                              │
  │  return Result.Failure(...)  │
  │─────────────────────────────►│
  │                              │  if (!result.IsSuccess)
  │                              │      return Results.NotFound(...)
  │                              │
  │  return Result.Success(dto)  │
  │─────────────────────────────►│
  │                              │  return Results.Ok(result.Value)
```

**Vantagens:**

| # | Vantagem | Detalhe |
|---|---|---|
| 1 | **Explícito** — a assinatura diz `Result<ProductDto>` em vez de `ProductDto` | Quem usa SABE que pode falhar |
| 2 | **Performance** — `return` é ~1000x mais rápido que `throw` | Zero stack unwinding |
| 3 | **Tipado** — cada tipo de erro é um valor, não uma exceção | `Error.NotFound(...)`, `Error.Validation(...)` |
| 4 | **Testável** — `Assert.True(result.IsFailure)` | Mais legível e natural |
| 5 | **Exceções ficam para bugs reais** | O `CustomExceptionHandler` só trata o inesperado |

---

## 3. Arquitetura proposta

### Onde cada peça vive

```
Shared.Contracts/                     ← Result Pattern vive aqui (contratos puros)
├── CQRS/
│   ├── ICommand.cs                   ← Passa a retornar Result<T>
│   ├── ICommandHandler.cs
│   ├── IQuery.cs                     ← Passa a retornar Result<T>
│   └── IQueryHandler.cs
└── Results/                          ← NOVO
    ├── Result.cs                     ← Result e Result<T>
    └── Error.cs                      ← Tipos de erro

Shared/
├── Behaviors/
│   └── ValidationBehavior.cs         ← Retorna Result.Failure em vez de throw
├── Exceptions/Handler/
│   └── CustomExceptionHandler.cs     ← Mantém, mas só trata exceções TÉCNICAS
└── Extensions/
    └── ResultExtensions.cs           ← NOVO: converte Result → IResult do MinimalAPI

Modules/*/
├── Features/*/
│   ├── *Handler.cs                   ← return Result.Success/Failure (sem throw)
│   ├── *Endpoint.cs                  ← Converte Result → HTTP response
│   └── *Validator.cs                 ← Sem mudança (FluentValidation continua igual)
```

### Fluxo completo com Result + i18n

```
1. Request chega com Accept-Language: pt-BR
              │
              ▼
2. UseRequestLocalization() → define CurrentUICulture = pt-BR
              │
              ▼
3. Carter Endpoint recebe o request
              │
              ▼
4. MediatR despacha o Command/Query
              │
              ▼
5. ValidationBehavior executa
   ├── Validação OK → continua para o handler
   └── Validação falhou → return Result.Failure(Error.Validation(mensagens localizadas))
              │
              ▼
6. Handler executa lógica de negócio
   ├── Sucesso → return Result.Success(dto)
   └── Falha → return Result.Failure(Error.NotFound(localizer["ProductNotFound", id]))
              │
              ▼
7. Endpoint converte Result → HTTP Response
   ├── result.IsSuccess → Results.Ok(result.Value)
   └── result.IsFailure → Results.NotFound(problemDetails localizado)
              │
              ▼
8. Cliente recebe:
   {
       "title": "Não Encontrado",
       "detail": "Produto com ID \"abc\" não foi encontrado.",
       "status": 404
   }
```

---

## 4. Implementação passo a passo

### Passo 1 — Criar `Error.cs` em `Shared.Contracts`

**Arquivo:** `src/Shared/Shared.Contracts/Results/Error.cs`

```csharp
namespace Shared.Contracts.Results;

/// <summary>
/// Represents a typed, structured error.
/// Immutable record — cada erro é um valor, não uma exceção.
/// </summary>
public sealed record Error
{
    private Error(string code, string message, ErrorType type)
    {
        Code = code;
        Message = message;
        Type = type;
    }

    /// <summary>
    /// Código identificador do erro (ex: "Product.NotFound", "Order.InvalidStatus").
    /// Útil para o frontend mapear erros sem depender da mensagem textual.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Mensagem legível para o usuário. É aqui que o i18n entra —
    /// essa mensagem vem do IStringLocalizer.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Tipo do erro — determina o HTTP status code no endpoint.
    /// </summary>
    public ErrorType Type { get; }

    // ──────────────────────────────────────────────
    //  Factory methods — criam erros tipados
    // ──────────────────────────────────────────────

    /// <summary>Recurso não encontrado → 404</summary>
    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    /// <summary>Erro de validação → 400</summary>
    public static Error Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    /// <summary>Requisição inválida por regra de negócio → 400</summary>
    public static Error BadRequest(string code, string message) =>
        new(code, message, ErrorType.BadRequest);

    /// <summary>Conflito (ex: recurso já existe) → 409</summary>
    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    /// <summary>Sem permissão → 403</summary>
    public static Error Forbidden(string code, string message) =>
        new(code, message, ErrorType.Forbidden);

    /// <summary>Erro genérico para quando nenhum tipo específico se aplica → 500</summary>
    public static Error Unexpected(string code, string message) =>
        new(code, message, ErrorType.Unexpected);

    /// <summary>
    /// Nenhum erro — usado internamente pelo Result.Success.
    /// NUNCA use diretamente.
    /// </summary>
    internal static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
}

/// <summary>
/// Mapeia diretamente para HTTP status codes nos endpoints.
/// </summary>
public enum ErrorType
{
    None = 0,
    NotFound = 404,
    Validation = 400,
    BadRequest = 400,
    Conflict = 409,
    Forbidden = 403,
    Unexpected = 500
}
```

**Por que `record` e não `class`?**
- Records têm igualdade por valor (`Error.NotFound("X", "Y") == Error.NotFound("X", "Y")`)
- São imutáveis por padrão
- `ToString()` automático para debug

**Por que `sealed`?**
- Ninguém deve herdar de `Error` — os factory methods cobrem todos os cenários

**Por que o `Code`?**
- O frontend pode usar o `Code` para mapear erros de forma estável
  (ex: `"Product.NotFound"` nunca muda, mas a `Message` muda por idioma)

---

### Passo 2 — Criar `Result.cs` em `Shared.Contracts`

**Arquivo:** `src/Shared/Shared.Contracts/Results/Result.cs`

```csharp
namespace Shared.Contracts.Results;

/// <summary>
/// Resultado de uma operação que NÃO retorna valor (equivalente a void).
/// Encapsula sucesso ou falha de forma explícita.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        // Invariante: sucesso não pode ter erro, falha não pode ser sem erro
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    /// <summary>Cria um resultado de sucesso sem valor.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Cria um resultado de falha.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Cria um resultado de sucesso com valor.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>Cria um resultado de falha tipado (sem valor).</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>
/// Resultado de uma operação que retorna um valor do tipo T.
/// Se IsSuccess, acesse Value. Se IsFailure, acesse Error.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// O valor retornado pela operação.
    /// Lança InvalidOperationException se acessado em caso de falha.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot access Value of a failed result. Error: {Error.Code} - {Error.Message}");

    /// <summary>Conversão implícita: permite retornar TValue direto como Result.</summary>
    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.Unexpected("Result.NullValue", "Value cannot be null."));
}
```

**Detalhes importantes:**

| Detalhe | Explicação |
|---|---|
| `throw` no construtor | São **invariantes de programação** (bug do dev), não erros de negócio — exceção é correta aqui |
| `throw` no `Value` getter | Acessar `Value` de um `Failure` é bug do dev — exceção é correta |
| `implicit operator` | Permite `return product;` em vez de `return Result.Success(product);` — syntactic sugar |
| `Result` (sem T) | Para commands que não retornam valor (ex: `DeleteProductCommand`) |
| `Result<T>` herda `Result` | Permite polimorfismo — behaviors tratam `Result` de forma genérica |

---

### Passo 3 — Atualizar interfaces CQRS

**Impacto:** As interfaces CQRS **não precisam mudar!** O `Result<T>` entra como o `TResponse`:

```csharp
// ANTES: public record GetProductQuery(Guid Id) : IQuery<ProductDto>;
// DEPOIS: public record GetProductQuery(Guid Id) : IQuery<Result<ProductDto>>;
```

As interfaces `IQuery<T>`, `ICommand<T>`, `IQueryHandler<TQuery, TResponse>`,
`ICommandHandler<TCommand, TResponse>` continuam exatamente iguais.
O `Result<T>` é apenas o tipo que preenche o generic `TResponse`.

O único ajuste **opcional** (mas recomendado) é a interface `ICommand` sem tipo:

**Arquivo:** `src/Shared/Shared.Contracts/CQRS/ICommand.cs`

```csharp
using MediatR;
using Shared.Contracts.Results;     // ← Adicionar

namespace Shared.Contracts.CQRS;

// Antes: ICommand : ICommand<Unit>
// Depois: ICommand : ICommand<Result>
// Motivo: commands sem retorno agora retornam Result (sucesso/falha) em vez de Unit (void)
public interface ICommand : ICommand<Result>
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
```

E o handler correspondente:

**Arquivo:** `src/Shared/Shared.Contracts/CQRS/ICommandHandler.cs`

```csharp
using MediatR;
using Shared.Contracts.Results;     // ← Adicionar

namespace Shared.Contracts.CQRS;

// Antes: ICommandHandler<TCommand> : ICommandHandler<TCommand, Unit>
// Depois: ICommandHandler<TCommand> : ICommandHandler<TCommand, Result>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Result>
    where TCommand : ICommand<Result>
{
}

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}
```

> **`IQuery` e `IQueryHandler` não mudam** — já são genéricos e o `Result<T>` é passado como tipo.

---

### Passo 4 — Adaptar o `ValidationBehavior`

**Arquivo:** `src/Shared/Shared/Behaviors/ValidationBehavior.cs`

O `ValidationBehavior` hoje **lança exceção** quando a validação falha.
Com Result Pattern, ele passa a **retornar** `Result.Failure`:

```csharp
using FluentValidation;
using MediatR;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;     // ← Adicionar

namespace Shared.Behaviors;

public class ValidationBehavior<TRequest, TResponse>
    (IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
    where TResponse : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => r.Errors.Count > 0)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count > 0)
        {
            // Concatena todas as mensagens de validação (já localizadas pelo FluentValidation)
            var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));

            // Cria um Result.Failure em vez de lançar exceção
            // O TResponse pode ser Result ou Result<T> — ambos herdam de Result
            return (dynamic)Result.Failure(
                Error.Validation("Validation.Failed", errorMessage));
        }

        return await next();
    }
}
```

**Explicação do `(dynamic)`:**
- `TResponse` pode ser `Result` ou `Result<ProductDto>` etc.
- `Result.Failure()` retorna `Result`, que precisa ser "cast" para `TResponse`
- Uma alternativa mais type-safe é usar reflection ou criar um `IResultFactory`,
  mas `(dynamic)` é a abordagem mais pragmática para behaviors genéricos

**Alternativa sem `dynamic` (mais segura):**

```csharp
if (failures.Count > 0)
{
    var error = Error.Validation("Validation.Failed", errorMessage);

    // Verifica se TResponse é Result<T> ou Result
    if (typeof(TResponse) == typeof(Result))
        return (TResponse)(object)Result.Failure(error);

    // Para Result<T>, usa reflection para chamar Result.Failure<T>(error)
    var resultType = typeof(TResponse).GetGenericArguments().FirstOrDefault();
    if (resultType is not null)
    {
        var failureMethod = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
            .MakeGenericMethod(resultType);

        return (TResponse)failureMethod.Invoke(null, [error])!;
    }

    throw new InvalidOperationException($"Unexpected TResponse type: {typeof(TResponse)}");
}
```

---

### Passo 5 — Criar `ResultExtensions.cs` para Carter Endpoints

**Arquivo novo:** `src/Shared/Shared/Extensions/ResultExtensions.cs`

Este extension method converte `Result<T>` para `IResult` do Minimal API (o tipo de retorno
que Carter/ASP.NET Core espera):

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Results;

namespace Shared.Extensions;

/// <summary>
/// Converte Result/Result<T> para IResult do ASP.NET Core Minimal API.
/// Usado nos Carter endpoints para mapear Result → HTTP Response.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converte Result<T> em IResult HTTP.
    /// Sucesso → 200 OK com valor.
    /// Falha → HTTP status baseado no ErrorType.
    /// </summary>
    public static IResult ToProblemResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        return result.Error.ToProblemDetails();
    }

    /// <summary>
    /// Converte Result (sem valor) em IResult HTTP.
    /// Sucesso → 204 No Content.
    /// Falha → HTTP status baseado no ErrorType.
    /// </summary>
    public static IResult ToProblemResult(this Result result)
    {
        if (result.IsSuccess)
            return Results.NoContent();

        return result.Error.ToProblemDetails();
    }

    /// <summary>
    /// Converte Result<T> em Created (201) em caso de sucesso.
    /// Útil para endpoints POST que criam recursos.
    /// </summary>
    public static IResult ToCreatedResult<T>(this Result<T> result, string uri)
    {
        if (result.IsSuccess)
            return Results.Created(uri, result.Value);

        return result.Error.ToProblemDetails();
    }

    // Converte Error → ProblemDetails com o status code correto
    private static IResult ToProblemDetails(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.BadRequest => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            statusCode: statusCode,
            title: error.Code,      // "Product.NotFound" — estável, não muda por idioma
            detail: error.Message   // "Produto não encontrado" — localizado via i18n
        );
    }
}
```

**Por que `Code` como `title` e não `Message`?**
- O `title` no ProblemDetails é para **identificação programática** — o frontend usa isso
- O `detail` é a **mensagem legível** para o usuário — essa sim é localizada

---

### Passo 6 — Adaptar o `CustomExceptionHandler`

Com o Result Pattern, o `CustomExceptionHandler` **continua existindo**, mas muda de papel:

| Antes | Depois |
|---|---|
| Trata **todos** os erros (negócio + técnicos) | Trata apenas erros **técnicos/inesperados** |
| `NotFoundException` → 404 | Não existe mais — virou `Result.Failure(Error.NotFound(...))` |
| `BadRequestException` → 400 | Não existe mais — virou `Result.Failure(Error.BadRequest(...))` |
| `ValidationException` → 400 | Não existe mais — `ValidationBehavior` retorna `Result.Failure` |
| `NullReferenceException` → 500 | ✅ Continua aqui — é um bug real |
| `DbException` → 500 | ✅ Continua aqui — é erro de infraestrutura |

**Arquivo simplificado:** `src/Shared/Shared/Exceptions/Handler/CustomExceptionHandler.cs`

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Shared.Resources;

namespace Shared.Exceptions.Handler;

/// <summary>
/// Trata APENAS exceções técnicas/inesperadas (bugs, infra, etc).
/// Erros de negócio são tratados via Result Pattern nos endpoints.
/// </summary>
public class CustomExceptionHandler
    (ILogger<CustomExceptionHandler> logger, IStringLocalizer<SharedMessages> localizer)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception,
            "Unhandled exception: {Message}, Time: {Time}",
            exception.Message, DateTime.UtcNow);

        var problemDetails = new ProblemDetails
        {
            Title = localizer["UnexpectedError"],
            Detail = "An internal error occurred. Please contact support.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path
        };

        problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        return true;
    }
}
```

> **Importante:** O `Detail` do 500 **não deve expor** `exception.Message` ao cliente em produção
> (pode vazar stack traces, connection strings, etc). Com o Result Pattern, isso é natural porque
> apenas erros inesperados chegam aqui.

---

## 5. Integração com i18n

### Onde cada coisa é localizada

```
┌───────────────────────────────────────────────────────────┐
│                       HANDLER                              │
│                                                            │
│  var product = await db.Products.FindAsync(id, ct);       │
│                                                            │
│  if (product is null)                                      │
│      return Result.Failure<ProductDto>(                    │
│          Error.NotFound(                                   │
│              "Product.NotFound",                           │ ← Code: NÃO localizado (estável)
│              string.Format(localizer["ProductNotFound"],   │ ← Message: LOCALIZADO via .resx
│                            id)));                          │
│                                                            │
│  return Result.Success(product.ToDto());                   │
└───────────────────────────────────────────────────────────┘
                         │
                         ▼
┌───────────────────────────────────────────────────────────┐
│                      ENDPOINT                              │
│                                                            │
│  var result = await sender.Send(new GetProductQuery(id));  │
│  return result.ToProblemResult();                          │
│          │                                                 │
│          ▼                                                 │
│  Se sucesso → 200 OK + dto                                │
│  Se falha   → ProblemDetails:                             │
│       title:  "Product.NotFound"    ← code (NÃO traduzido)│
│       detail: "Produto com ID..."   ← message (traduzido) │
│       status: 404                   ← do ErrorType         │
└───────────────────────────────────────────────────────────┘
```

### Exemplo completo com i18n: Handler → Endpoint → Response

**Handler (Catalog module):**
```csharp
public class GetProductHandler(
    IStringLocalizer<CatalogMessages> localizer,
    CatalogDbContext db)
    : IQueryHandler<GetProductQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductQuery query, CancellationToken ct)
    {
        var product = await db.Products.FindAsync(query.Id, ct);

        if (product is null)
            return Result.Failure<ProductDto>(
                Error.NotFound(
                    "Product.NotFound",
                    string.Format(localizer["ProductNotFound"], query.Id)));

        return product.ToDto();  // implicit conversion → Result.Success(dto)
    }
}
```

**Endpoint (Carter module):**
```csharp
public class GetProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProductQuery(id));
            return result.ToProblemResult();
        });
    }
}
```

**Respostas por idioma:**

`Accept-Language: en-US`
```json
{
    "title": "Product.NotFound",
    "detail": "Product with ID \"abc-123\" was not found.",
    "status": 404
}
```

`Accept-Language: pt-BR`
```json
{
    "title": "Product.NotFound",
    "detail": "Produto com ID \"abc-123\" não foi encontrado.",
    "status": 404
}
```

`Accept-Language: es`
```json
{
    "title": "Product.NotFound",
    "detail": "El producto con ID \"abc-123\" no fue encontrado.",
    "status": 404
}
```

> Note que o `title` é sempre `"Product.NotFound"` independente do idioma.
> Isso permite que o frontend faça `if (error.title === "Product.NotFound")` de forma confiável.

---

## 6. Impacto em cada camada do projeto

### Mapa completo de mudanças

```
NOVOS ARQUIVOS:
  📄 src/Shared/Shared.Contracts/Results/Error.cs
  📄 src/Shared/Shared.Contracts/Results/Result.cs
  📄 src/Shared/Shared/Extensions/ResultExtensions.cs

ARQUIVOS EDITADOS:
  ✏️ src/Shared/Shared.Contracts/CQRS/ICommand.cs
     • ICommand : ICommand<Unit> → ICommand<Result>
  ✏️ src/Shared/Shared.Contracts/CQRS/ICommandHandler.cs
     • ICommandHandler<T> : ICommandHandler<T, Unit> → ICommandHandler<T, Result>
  ✏️ src/Shared/Shared/Behaviors/ValidationBehavior.cs
     • throw ValidationException → return Result.Failure(Error.Validation(...))
  ✏️ src/Shared/Shared/Exceptions/Handler/CustomExceptionHandler.cs
     • Simplificado: só trata exceções técnicas (500)

SEM MUDANÇAS:
  ─ src/Shared/Shared.Contracts/CQRS/IQuery.cs          (já é genérico)
  ─ src/Shared/Shared.Contracts/CQRS/IQueryHandler.cs   (já é genérico)
  ─ src/Shared/Shared/Behaviors/LoggingBehavior.cs       (funciona com qualquer TResponse)
  ─ src/Shared/Shared/Extensions/MediatRExtensions.cs    (registra behaviors genéricos)
  ─ src/Shared/Shared/Extensions/CarterExtensions.cs     (registra modules)
  ─ src/Bootstrapper/Api/Program.cs                      (sem mudança)
  ─ Todos os .resx                                       (sem mudança)
  ─ Todas as marker classes                              (sem mudança)

PODEM SER REMOVIDOS (após migração completa):
  🗑️ src/Shared/Shared/Exceptions/NotFoundException.cs
  🗑️ src/Shared/Shared/Exceptions/BadRequestException.cs
  🗑️ src/Shared/Shared/Exceptions/InternalServerException.cs
  (Só remova quando nenhum código referenciar mais essas exceções)
```

### Por camada

| Camada | Antes (com exceções) | Depois (com Result) |
|---|---|---|
| **Shared.Contracts** | Interfaces CQRS puras | + `Error`, `Result`, `Result<T>` |
| **Shared (Behaviors)** | `ValidationBehavior` lança exceção | `ValidationBehavior` retorna `Result.Failure` |
| **Shared (Exceptions)** | 4 exception classes + handler complexo | Handler simplificado (só 500) + exception classes opcionais |
| **Shared (Extensions)** | `MediatRExtensions`, `CarterExtensions` | + `ResultExtensions` (Result → IResult HTTP) |
| **Modules (Handlers)** | `throw new NotFoundException(...)` | `return Result.Failure(Error.NotFound(...))` |
| **Modules (Endpoints)** | Não precisa tratar erros (exceção sobe) | `result.ToProblemResult()` — decisão explícita |
| **Modules (Validators)** | Sem mudança | Sem mudança (FluentValidation continua igual) |
| **Program.cs** | Sem mudança | Sem mudança |
| **i18n (.resx)** | Sem mudança | Sem mudança |

---

## 7. Antes vs Depois — comparação completa

### Handler — Antes

```csharp
public class GetProductHandler(CatalogDbContext db)
    : IQueryHandler<GetProductQuery, ProductDto>
{
    public async Task<ProductDto> Handle(
        GetProductQuery query, CancellationToken ct)
    {
        var product = await db.Products.FindAsync(query.Id, ct)
            ?? throw new NotFoundException("Product", query.Id);
        //         ↑ EXCEÇÃO — fluxo invisível, sobe até o handler global

        return product.ToDto();
    }
}
```

### Handler — Depois

```csharp
public class GetProductHandler(
    IStringLocalizer<CatalogMessages> localizer,
    CatalogDbContext db)
    : IQueryHandler<GetProductQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductQuery query, CancellationToken ct)
    {
        var product = await db.Products.FindAsync(query.Id, ct);

        if (product is null)
            return Result.Failure<ProductDto>(
                Error.NotFound("Product.NotFound",
                    string.Format(localizer["ProductNotFound"], query.Id)));
        //  ↑ RESULTADO — explícito, tipado, localizado

        return product.ToDto();  // implicit → Result.Success(dto)
    }
}
```

### Endpoint — Antes

```csharp
app.MapGet("/api/products/{id:guid}", async (Guid id, ISender sender) =>
{
    var product = await sender.Send(new GetProductQuery(id));
    // Se NotFoundException → voa para CustomExceptionHandler (implícito)
    return Results.Ok(product);
});
```

### Endpoint — Depois

```csharp
app.MapGet("/api/products/{id:guid}", async (Guid id, ISender sender) =>
{
    var result = await sender.Send(new GetProductQuery(id));
    return result.ToProblemResult();
    // Se sucesso → 200 OK com dto
    // Se falha → ProblemDetails com status correto (explícito)
});
```

### Validator — Sem mudança!

```csharp
// Continua exatamente igual — o ValidationBehavior é quem converte para Result
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator(IStringLocalizer<CatalogMessages> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(localizer["ProductNameRequired"]);
    }
}
```

### Teste — Antes

```csharp
[Fact]
public async Task GetProduct_NotFound_ThrowsNotFoundException()
{
    // Arrange ...

    // Act & Assert — menos natural
    await Assert.ThrowsAsync<NotFoundException>(() =>
        handler.Handle(query, CancellationToken.None));
}
```

### Teste — Depois

```csharp
[Fact]
public async Task GetProduct_NotFound_ReturnsFailure()
{
    // Arrange ...

    // Act
    var result = await handler.Handle(query, CancellationToken.None);

    // Assert — mais natural e legível
    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.NotFound, result.Error.Type);
    Assert.Equal("Product.NotFound", result.Error.Code);
}
```

---

## 8. Regras e boas práticas

### ✅ FAZER

| Regra | Motivo |
|---|---|
| Usar `Error.NotFound(code, message)` com **código estável** e **mensagem localizada** | `Code` para frontend, `Message` para humanos |
| Convençao de códigos: `"Módulo.Ação"` (ex: `"Product.NotFound"`, `"Order.InvalidStatus"`) | Consistência e fácil busca |
| Manter o `CustomExceptionHandler` para exceções técnicas | Bugs reais ainda precisam de catch global |
| Testar `result.IsSuccess` / `result.IsFailure` nos unit tests | Mais legível que `Assert.ThrowsAsync` |
| Usar `implicit operator` com moderação | `return dto;` é limpo, mas `return Result.Success(dto)` é mais explícito |

### ❌ NÃO FAZER

| Anti-padrão | Problema |
|---|---|
| `throw` para erros de negócio nos handlers | Volta para o padrão antigo — use `Result.Failure` |
| `result.Value` sem verificar `result.IsSuccess` | Lança exceção — é um bug do desenvolvedor |
| `Error.Unexpected` para erros de negócio | Use o tipo correto (`NotFound`, `Validation`, etc.) |
| Localizar o `Code` do `Error` | O `Code` é para identificação programática — deve ser estável |
| Ignorar o `Result` no endpoint | `var _ = await sender.Send(command);` perde o erro silenciosamente |

### ⚠️ Migração gradual

Você **não precisa migrar tudo de uma vez**. Pode manter exceções e Result Pattern coexistindo:

1. Crie `Error.cs`, `Result.cs`, `ResultExtensions.cs`
2. Mude `ICommand`/`ICommandHandler` para `Result`
3. Adapte o `ValidationBehavior`
4. Migre handlers **um a um** — comece pelo mais simples
5. Quando nenhum handler usar exceções de negócio, remova as exception classes

---

## 9. FAQ

### "Posso usar Result e exceções ao mesmo tempo durante a migração?"

Sim! O `CustomExceptionHandler` continua capturando exceções. Novos handlers usam `Result`,
e os antigos continuam lançando exceções até serem migrados.

### "E se meu handler precisar chamar outro serviço que lança exceção?"

Wrap com try-catch e converta para `Result`:
```csharp
try
{
    await externalService.DoSomething();
    return Result.Success();
}
catch (HttpRequestException ex)
{
    return Result.Failure(Error.Unexpected("ExternalService.Failed", ex.Message));
}
```

### "O `LoggingBehavior` precisa mudar?"

Não! Ele recebe `TResponse` genérico e simplesmente loga + repassa. Funciona com `Result<T>`
exatamente como funcionava com `ProductDto`.

### "E o `MediatRExtensions` precisa mudar?"

Não! Ele registra behaviors como open generics (`typeof(ValidationBehavior<,>)`).
Funciona com qualquer `TResponse`, incluindo `Result<T>`.

### "Preciso de uma library externa como FluentResults ou ErrorOr?"

Não. O Result Pattern é simples o suficiente para implementar in-house com ~80 linhas de código.
Libraries externas (FluentResults, ErrorOr, Ardalis.Result) adicionam dependências desnecessárias
e features que você provavelmente não vai usar. Se no futuro precisar de algo mais sofisticado
(como `Result.Combine`, `Result.Map`, `Result.Bind`), você adiciona ao seu próprio `Result.cs`.

### "O implicit operator de `Result<T>` não é perigoso?"

Em 99% dos casos é uma conveniência segura:
```csharp
return product.ToDto();  // implicitamente: Result.Success(dto)
```
O único risco é retornar `null` sem querer — o operator trata isso retornando `Failure`.
Se preferir ser sempre explícito, não use o implicit e sempre escreva `Result.Success(dto)`.

---

## Checklist de implementação

- [ ] Criar `src/Shared/Shared.Contracts/Results/Error.cs`
- [ ] Criar `src/Shared/Shared.Contracts/Results/Result.cs`
- [ ] Editar `src/Shared/Shared.Contracts/CQRS/ICommand.cs` — `Unit` → `Result`
- [ ] Editar `src/Shared/Shared.Contracts/CQRS/ICommandHandler.cs` — `Unit` → `Result`
- [ ] Editar `src/Shared/Shared/Behaviors/ValidationBehavior.cs` — `throw` → `return Result.Failure`
- [ ] Criar `src/Shared/Shared/Extensions/ResultExtensions.cs`
- [ ] Simplificar `src/Shared/Shared/Exceptions/Handler/CustomExceptionHandler.cs`
- [ ] Migrar handlers existentes um a um
- [ ] (Opcional) Remover exception classes quando não forem mais usadas
