using RestaurantHandler.Utilities;

namespace RestaurantHandler.Mcp;

/// <summary>Liens vers le front renvoyés par les outils MCP, et nettoyage des paramètres texte.</summary>
public static class McpPublicLinks
{
    /// <summary>Slug de la fiche d'aide qui documente le serveur (<c>help-entries-mcp.json</c>).</summary>
    public const string DocumentationSlug = "serveur-mcp-bonprof";

    public static string FrontUrl => (EnvironmentVariables.API_FRONT_URL ?? "").TrimEnd('/');

    public static string ProfileUrl(Guid teacherId) => $"{FrontUrl}/profile/{teacherId}";

    public static string DocumentationUrl => $"{FrontUrl}/aide/{DocumentationSlug}";

    public static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
