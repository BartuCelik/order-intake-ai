using Microsoft.OpenApi;
using OrderIntake.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi(options =>
{
    // Power Automate custom connectors only import Swagger 2.0.
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi2_0;
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Order Intake AI";
        document.Info.Description = "Extracts structured order data from PDF purchase orders.";
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<ApiKeyDocumentTransformer>();
});

// Fails at startup, not on the first request, when the key is missing or too short.
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapHealthChecks("/health");
app.MapOpenApi(); // /openapi/v1.json, anonymous (see ApiKeyMiddleware)

app.Run();
