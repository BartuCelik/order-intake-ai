using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OrderIntake.Api.Security;

/// <summary>
/// Declares the <c>X-Api-Key</c> header as the document-wide security scheme,
/// so the Power Automate connector import sets up API key authentication by itself.
/// </summary>
public sealed class ApiKeyDocumentTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeId = "ApiKey";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyMiddleware.HeaderName,
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeId, document)] = [] });

        return Task.CompletedTask;
    }
}
