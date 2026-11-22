using System.Threading.RateLimiting;
using RestaurantHandler.Utilities;
using Microsoft.AspNetCore.RateLimiting;

namespace RestaurantHandler.Mcp;

/// <summary>
/// Serveur MCP public (Streamable HTTP, stateless) sur <c>/mcp</c> et son manifeste de
/// découverte sur <c>/.well-known/mcp-server</c>.
/// </summary>
public static class McpServerSetup
{
    public const string Route = "/mcp";
    public const string RateLimitPolicy = "mcp";
    public const int PermitsPerMinute = 60;

    private static readonly string[] ManifestRoutes = ["/.well-known/mcp-server", "/.well-known/mcp.json"];

    private static string Version => Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";

    public static IServiceCollection AddBonProfMcp(this IServiceCollection services)
    {
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "bonprof",
                    Title = "BonProf",
                    Version = Version,
                    WebsiteUrl = "https://bonprof.fr",
                };
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<TeacherSearchTools>()
            .WithTools<TeacherCourseTools>()
            .WithTools<TeacherContactTools>();

        services.AddHttpContextAccessor();
        services.AddSingleton<McpContactLimiter>();

        // Partitionné par IP client : RemoteIpAddress est celle de l'appelant tant que
        // UseForwardedHeaders fait confiance au réseau du reverse proxy (Program.cs).
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(
                RateLimitPolicy,
                context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = PermitsPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }
                    )
            );
        });

        return services;
    }

    public static void MapBonProfMcp(this WebApplication app)
    {
        app.MapMcp(Route).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);

        foreach (var route in ManifestRoutes)
        {
            app.MapGet(route, () => Results.Ok(Manifest()))
                .AllowAnonymous()
                .ExcludeFromDescription();
        }
    }

    // URL publique configurée plutôt que déduite de la requête : derrière le reverse proxy, le
    // schéma et l'hôte vus par l'API sont ceux du saut interne (http, conteneur).
    private static object Manifest() =>
        new
        {
            name = "bonprof",
            title = "BonProf",
            description =
                "Recherche de professeurs particuliers sur BonProf, consultation de leurs cours et prise de contact.",
            version = Version,
            websiteUrl = "https://bonprof.fr",
            documentationUrl = McpPublicLinks.DocumentationUrl,
            transport = new
            {
                type = "streamable-http",
                url = $"{(EnvironmentVariables.API_BACK_URL ?? "").TrimEnd('/')}{Route}",
            },
            authentication = new { required = false },
            tools = new[]
            {
                new
                {
                    name = TeacherSearchTools.FastSearchName,
                    description = TeacherSearchTools.FastSearchDescription,
                },
                new
                {
                    name = TeacherCourseTools.GetTeacherCoursesName,
                    description = TeacherCourseTools.GetTeacherCoursesDescription,
                },
                new
                {
                    name = TeacherContactTools.ContactTeacherName,
                    description = TeacherContactTools.ContactTeacherDescription,
                },
            },
        };
}
