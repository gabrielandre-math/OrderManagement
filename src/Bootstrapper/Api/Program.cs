using System.Globalization;
using Carter;
using Catalog;
using Catalog.Data;
using Basket;
using Basket.Data;
using Orders;
using Orders.Data;
using Shared.Data.Interceptors;
using Shared.Exceptions.Handler;
using Shared.Extensions;
using Shared.Messaging.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// ──────────────────────────────────────────────
//  Cross-cutting services
// ──────────────────────────────────────────────

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Safety net: catches any unhandled exceptions and returns ProblemDetails with i18n
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();

// i18n: Register localization services
builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] {
        "en-US", "pt-BR", "es", "fr", "ja", "zh-CN", "zh-TW", "ko", "de", "it",
        "ru", "ar", "hi", "pl", "nl", "sv", "da", "no", "fi", "cs", "hu", "ro",
        "bg", "el", "tr", "th", "vi", "id", "ms", "uk", "sk", "hr", "sr", "sl",
        "lt", "lv", "et", "he", "fa", "bn", "sw", "ca", "eu"
    }.Select(c => new CultureInfo(c)).ToList();

    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});

// EF Core interceptors (resolved via DI)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditableEntityInterceptor>();
builder.Services.AddScoped<DispatchDomainEventsInterceptor>();

// ──────────────────────────────────────────────
//  Module assemblies (add once, used everywhere)
// ──────────────────────────────────────────────

var catalogAssembly = typeof(CatalogModule).Assembly;
var basketAssembly = typeof(BasketModule).Assembly;
var ordersAssembly = typeof(OrdersModule).Assembly;

// CQRS + Validation + Logging Pipelines
builder.Services.AddMediatRWithAssemblies(catalogAssembly, basketAssembly, ordersAssembly);

// Carter Endpoints
builder.Services.AddCarterWithAssemblies(catalogAssembly, basketAssembly, ordersAssembly);

// MassTransit + RabbitMQ (integration events between modules)
builder.Services.AddMassTransitWithAssemblies(
    builder.Configuration, catalogAssembly, basketAssembly, ordersAssembly);

// ──────────────────────────────────────────────
//  Module registrations (DbContexts, module-specific DI)
// ──────────────────────────────────────────────

builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddBasketModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);

var app = builder.Build();

// ──────────────────────────────────────────────
//  Middleware pipeline
// ──────────────────────────────────────────────

// Swagger: enable in Development or when explicitly configured
var enableSwagger = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("EnableSwagger");

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Safety net: must be before other middleware to catch unhandled exceptions
app.UseExceptionHandler();

// i18n: Enable request localization middleware (must be before endpoints)
app.UseRequestLocalization();
app.UseHttpsRedirection();

// Carter: map all module endpoints
app.MapCarter();

// ──────────────────────────────────────────────
//  Auto-migrate + seed on startup
// ──────────────────────────────────────────────

await app.UseMigrationAsync<CatalogDbContext>();
await app.UseMigrationAsync<BasketDbContext>();
await app.UseMigrationAsync<OrdersDbContext>();

app.Run();

