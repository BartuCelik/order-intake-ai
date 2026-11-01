using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace OrderIntake.Api.Security;

/// <summary>
/// Rejects every request without a valid <c>X-Api-Key</c> header with a 401 ProblemDetails.
/// <c>/health</c> and <c>/openapi</c> stay anonymous: the host's health probe and the connector import cannot send the key.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options, IProblemDetailsService problemDetails)
{
    public const string HeaderName = "X-Api-Key";

    private static readonly PathString[] AnonymousPaths = [new("/health"), new("/openapi")];

    // Both sides are hashed to 32 bytes, so the constant-time comparison does not leak the key's length either.
    private readonly byte[] _expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Key));

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsAnonymous(context.Request.Path) || IsValid(context.Request.Headers[HeaderName]))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = $"Send a valid API key in the {HeaderName} header.",
            },
        });
    }

    private static bool IsAnonymous(PathString path) =>
        AnonymousPaths.Any(anonymous => path.StartsWithSegments(anonymous, StringComparison.OrdinalIgnoreCase));

    private bool IsValid(StringValues values)
    {
        // A repeated header is ambiguous; reject it rather than pick one.
        if (values.Count != 1 || string.IsNullOrEmpty(values[0]))
        {
            return false;
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(values[0]!));
        return CryptographicOperations.FixedTimeEquals(actualHash, _expectedHash);
    }
}
