using System.ComponentModel.DataAnnotations;

namespace OrderIntake.Api.Security;

/// <summary>The shared key the Power Automate custom connector sends. Set via user-secrets or app settings, never appsettings.json.</summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    [Required, MinLength(32)]
    public string Key { get; init; } = "";
}
