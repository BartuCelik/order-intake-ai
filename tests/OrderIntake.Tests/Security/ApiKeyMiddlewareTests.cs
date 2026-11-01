using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderIntake.Api.Security;

namespace OrderIntake.Tests.Security;

public sealed class ApiKeyMiddlewareTests
{
    private const string Key = "test-key-0123456789-0123456789-0123456789";

    private static readonly IServiceProvider Services = new ServiceCollection().AddOptions().AddProblemDetails().BuildServiceProvider();

    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_ValidKey_CallsNext()
    {
        var context = CreateContext("/orders/parse", Key);

        await CreateMiddleware().InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData(Key + "x")]
    public async Task InvokeAsync_MissingOrWrongKey_Returns401ProblemDetails(string? key)
    {
        var context = CreateContext("/orders/parse", key);

        await CreateMiddleware().InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_RepeatedHeader_Returns401()
    {
        var context = CreateContext("/orders/parse", key: null);
        context.Request.Headers[ApiKeyMiddleware.HeaderName] = new[] { Key, Key };

        await CreateMiddleware().InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/openapi/v1.json")]
    public async Task InvokeAsync_AnonymousPathWithoutKey_CallsNext(string path)
    {
        var context = CreateContext(path, key: null);

        await CreateMiddleware().InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PathThatOnlyStartsLikeAnonymous_Returns401()
    {
        var context = CreateContext("/healthcheck-bypass", key: null);

        await CreateMiddleware().InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private ApiKeyMiddleware CreateMiddleware() =>
        new(
            _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new ApiKeyOptions { Key = Key }),
            Services.GetRequiredService<IProblemDetailsService>());

    private static DefaultHttpContext CreateContext(string path, string? key)
    {
        var context = new DefaultHttpContext { RequestServices = Services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (key is not null)
        {
            context.Request.Headers[ApiKeyMiddleware.HeaderName] = key;
        }

        return context;
    }
}
