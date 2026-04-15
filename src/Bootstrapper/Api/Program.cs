using System.Globalization;
using Shared.Exceptions.Handler;

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

var app = builder.Build();

// Configure the HTTP request pipeline.

// Safety net: must be before other middleware to catch unhandled exceptions
app.UseExceptionHandler();

// i18n: Enable request localization middleware (must be before endpoints)
app.UseRequestLocalization();

app.UseHttpsRedirection();

app.Run();

