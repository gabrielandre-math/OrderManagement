using System.Globalization;
using Carter;
using Catalog;
using Catalog.Data;
using Shared.Data.Interceptors;
using Shared.Exceptions.Handler;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// CQRS + Validation + Loggin Pipelines
var catalogAssembly = typeof(CatalogModule).Assembly;
builder.Services.AddMediatRWithAssemblies(catalogAssembly);

// Carter Endpoints
builder.Services.AddCarterWithAssemblies(catalogAssembly);

// EF Core interceptors (resolved via DI)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditableEntityInterceptor>();
builder.Services.AddScoped<DispatchDomainEventsInterceptor>();

// Modules
builder.Services.AddCatalogModule(builder.Configuration);


var app = builder.Build();

// Configure the HTTP request pipeline.

// Swagger: enable in Development environment
if (app.Environment.IsDevelopment())
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

// Auto-migrate + seed on startup (optional, can be removed in production)
await app.UseMigrationAsync<CatalogDbContext>();

app.Run();

