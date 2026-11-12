using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderIntake.Api.Security;

namespace OrderIntake.Tests.OpenApi;

public sealed class OpenApiDocumentTests(OpenApiDocumentTests.ApiFactory factory) : IClassFixture<OpenApiDocumentTests.ApiFactory>
{
    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "options", "head", "patch"];

    [Fact]
    public async Task GetDocument_WithoutApiKey_ReturnsSwagger20()
    {
        using var document = await GetDocumentAsync();

        Assert.Equal("2.0", document.RootElement.GetProperty("swagger").GetString());
    }

    [Fact]
    public async Task GetDocument_Always_DeclaresApiKeyHeaderAsGlobalSecurity()
    {
        using var document = await GetDocumentAsync();

        var scheme = document.RootElement.GetProperty("securityDefinitions").GetProperty(ApiKeyDocumentTransformer.SchemeId);
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal(ApiKeyMiddleware.HeaderName, scheme.GetProperty("name").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("security").EnumerateArray(),
            requirement => requirement.TryGetProperty(ApiKeyDocumentTransformer.SchemeId, out _));
    }

    // Power Automate names connector actions after operationId and shows the summary; both must be set explicitly.
    [Fact]
    public async Task GetDocument_EveryOperation_HasOperationIdAndSummary()
    {
        using var document = await GetDocumentAsync();

        var operations = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Where(item => HttpMethods.Contains(item.Name)).Select(item => (Name: $"{item.Name} {path.Name}", Operation: item.Value)));
        foreach (var (name, operation) in operations)
        {
            Assert.False(string.IsNullOrWhiteSpace(operation.TryGetProperty("operationId", out var id) ? id.GetString() : null), $"{name} has no operationId.");
            Assert.False(string.IsNullOrWhiteSpace(operation.TryGetProperty("summary", out var summary) ? summary.GetString() : null), $"{name} has no summary.");
        }
    }

    private async Task<JsonDocument> GetDocumentAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ApiKey:Key", "test-key-0123456789-0123456789-0123456789");
    }
}
