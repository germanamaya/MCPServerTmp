namespace MCPServer.Auth;

/// <summary>
/// Settings for validating OAuth 2.0 bearer tokens issued by an OpenID Connect authority
/// such as Microsoft Entra ID. Bound from the <c>Authentication</c> configuration section.
/// </summary>
public sealed class AuthSettings
{
    public const string SectionName = "Authentication";

    /// <summary>Issuer base URL, for example <c>https://login.microsoftonline.com/{tenantId}/v2.0</c>.</summary>
    public string? Authority { get; set; }

    /// <summary>Accepted <c>aud</c> values. At least one is required.</summary>
    public string[] Audiences { get; set; } = [];

    /// <summary>Delegated scopes advertised to clients and accepted in the token's scope claim.</summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>Application roles accepted in the token's <c>roles</c> claim, for callers without a signed-in user.</summary>
    public string[] Roles { get; set; } = [];

    public IReadOnlyList<string> GetConfigurationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Authority))
        {
            errors.Add($"{SectionName}:Authority is not set.");
        }
        else if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authority) || authority.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"{SectionName}:Authority must be an absolute HTTPS URL.");
        }

        if (!Audiences.Any(audience => !string.IsNullOrWhiteSpace(audience)))
        {
            errors.Add($"{SectionName}:Audiences must contain at least one value.");
        }

        return errors;
    }

    public bool IsConfigured => GetConfigurationErrors().Count == 0;
}
