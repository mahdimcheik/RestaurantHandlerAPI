namespace RestaurantHandler.Utilities;

public static class EnvironmentVariables
{
    private static string GetEnvVar(string envVarName, string fallbackValue)
    {
        return Environment.GetEnvironmentVariable(envVarName) ?? fallbackValue;
    }

    private static string GetRequiredEnvVar(string envVarName)
    {
        var value = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"La variable d'environnement {envVarName} est obligatoire."
            );
        }
        return value;
    }

    /// <summary>
    /// Secrets et comptes sans lesquels l'application ne doit pas démarrer. Aucun n'a de valeur
    /// de repli : une valeur publiée dans le dépôt permettrait de forger un jeton administrateur.
    /// </summary>
    public static readonly string[] RequiredAtStartup =
    [
        "JWT_KEY",
        "DB_PASSWORD",
        "ADMIN_EMAIL",
        "ADMIN_PASSWORD",
    ];

    public static void ValidateRequired() => ValidateRequired(Environment.GetEnvironmentVariable);

    public static void ValidateRequired(Func<string, string?> getVariable)
    {
        var errors = RequiredAtStartup
            .Where(name => string.IsNullOrWhiteSpace(getVariable(name)))
            .Select(name => $"variable manquante : {name}")
            .ToList();

        var addressKey = getVariable("ADDRESS_ENCRYPTION_KEY");
        if (!string.IsNullOrWhiteSpace(addressKey) && !FieldEncryption.IsValidKey(addressKey))
        {
            errors.Add("ADDRESS_ENCRYPTION_KEY doit être 32 octets encodés en base64");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuration invalide : " + string.Join(" ; ", errors)
            );
        }
    }

    private static int GetEnvVarInt(string envVarName, int fallbackValue)
    {
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        return int.TryParse(envValue, out var result) ? result : fallbackValue;
    }

    private static bool GetEnvVarBool(string envVarName, bool fallbackValue)
    {
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        return bool.TryParse(envValue, out var result) ? result : fallbackValue;
    }

    private static Guid GetEnvVarGuid(string envVarName, string fallBackValue)
    {
        var value = Environment.GetEnvironmentVariable(envVarName);
        return Guid.TryParse(value, out var result) ? result : Guid.Parse(fallBackValue);
    }

    public static string JWT_KEY => GetRequiredEnvVar("JWT_KEY");

    // Clé AES-256 (base64, 32 octets) du chiffrement au repos (FieldEncryption). Lue seulement
    // au premier chiffrement ; la perdre rend les champs chiffrés illisibles.
    public static string ADDRESS_ENCRYPTION_KEY => GetRequiredEnvVar("ADDRESS_ENCRYPTION_KEY");
    public static string? API_BACK_URL => GetEnvVar("API_BACK_URL", "");
    public static string? API_FRONT_URL => GetEnvVar("API_FRONT_URL", "");

    /// <summary>
    /// Clés VAPID des notifications natives (Web Push). Facultatives : sans la paire, aucune
    /// notification native n'est envoyée et le front masque l'option.
    /// </summary>
    public static string VAPID_PUBLIC_KEY => GetEnvVar("VAPID_PUBLIC_KEY", "");
    public static string VAPID_PRIVATE_KEY => GetEnvVar("VAPID_PRIVATE_KEY", "");

    /// <summary>Contact transmis aux services de push (mailto: ou https:).</summary>
    public static string VAPID_SUBJECT => GetEnvVar("VAPID_SUBJECT", "mailto:contact@bonprof.fr");

    /// <summary>
    /// Ouvre le tableau de bord Hangfire aux requêtes de la machine elle-même, sans session. Pris
    /// en compte en Development seulement : derrière un proxy inverse local, toute requête paraît
    /// locale.
    /// </summary>
    public static bool HANGFIRE_DASHBOARD_ALLOW_LOCAL =>
        GetEnvVarBool("HANGFIRE_DASHBOARD_ALLOW_LOCAL", false);

    /// <summary>
    /// Journalise le SQL exécuté par EF Core (niveau Information). Diagnostic de performance
    /// uniquement : verbeux, à ne jamais activer en production.
    /// </summary>
    public static bool LOG_SQL => GetEnvVarBool("LOG_SQL", false);

    // RATE LIMITING (Infrastructure/RateLimiting/RateLimitingSetup.cs)
    public static bool RATE_LIMITING_ENABLED => GetEnvVarBool("RATE_LIMITING_ENABLED", true);

    /// <summary>Requêtes par minute et par adresse IP, toutes routes confondues.</summary>
    public static int RATE_LIMIT_IP_PER_MINUTE => GetEnvVarInt("RATE_LIMIT_IP_PER_MINUTE", 300);

    /// <summary>Requêtes par minute et par utilisateur connecté, quelle que soit son adresse.</summary>
    public static int RATE_LIMIT_USER_PER_MINUTE => GetEnvVarInt("RATE_LIMIT_USER_PER_MINUTE", 200);

    /// <summary>
    /// Tentatives par minute et par adresse IP sur la connexion, l'inscription et la
    /// réinitialisation du mot de passe.
    /// </summary>
    public static int RATE_LIMIT_AUTH_PER_MINUTE => GetEnvVarInt("RATE_LIMIT_AUTH_PER_MINUTE", 10);

    /// <summary>
    /// Adresses publiques jamais limitées, séparées par des virgules : celle du serveur de rendu
    /// (SSR), qui appelle l'API pour tous ses visiteurs depuis une seule adresse.
    /// </summary>
    public static string RATE_LIMIT_EXEMPT_IPS => GetEnvVar("RATE_LIMIT_EXEMPT_IPS", "");

    // DATABASE
    public static string DB_PORT => GetEnvVar("DB_PORT", "5432");
    public static string DB_HOST => GetEnvVar("DB_HOST", "localhost");
    public static string DB_NAME => GetEnvVar("DB_NAME", "mainDB");
    public static string DB_USER => GetEnvVar("DB_USER", "postgres");
    public static string DB_PASSWORD => GetRequiredEnvVar("DB_PASSWORD");

    // auth
    public static int COOKIES_VALIDITY_DAYS => GetEnvVarInt("COOKIES_VALIDITY_DAYS", 7);

    /// <summary>false uniquement en dev sans HTTPS : un cookie Secure n'est jamais renvoyé sur http://.</summary>
    public static bool COOKIES_SECURE => GetEnvVarBool("COOKIES_SECURE", true);
    public static int TOKEN_VALIDITY_MINUTES => GetEnvVarInt("TOKEN_VALIDITY_MINUTES", 30);

    // google oauth : client id de l'application web, sert d'audience attendue lors de la
    // validation de l'ID token envoyé par le front (aucun client_secret nécessaire).
    public static string GOOGLE_CLIENT_ID => GetEnvVar("GOOGLE_CLIENT_ID", "");

    // mail
    public static string SMTP_HOST => GetEnvVar("SMTP_HOST", "smtp.mailtrap.io");
    public static int SMTP_PORT => GetEnvVarInt("SMTP_PORT", 587);
    public static string SMTP_LOGIN => GetEnvVar("SMTP_LOGIN", "");
    public static string SMTP_KEY => GetEnvVar("SMTP_KEY", "");

    /// <summary>
    /// true uniquement pour un serveur SMTP joint par le réseau Docker interne (Stalwart) :
    /// son certificat est auto-signé et ne porte pas le nom du conteneur.
    /// </summary>
    public static bool SMTP_ALLOW_SELF_SIGNED => GetEnvVarBool("SMTP_ALLOW_SELF_SIGNED", false);

    public static string DO_NO_REPLY_MAIL => GetEnvVar("DO_NO_REPLY_MAIL", "ne-pas-repondre@bonprof.fr");

    // compte Manager initial (AdminAccountsSeeder)
    public static string ADMIN_EMAIL => GetRequiredEnvVar("ADMIN_EMAIL");
    public static string ADMIN_PASSWORD => GetRequiredEnvVar("ADMIN_PASSWORD");
   
    // Stockage objet (ADR-0008) : un compartiment compatible S3 (OVH Object Storage). avatars/ est
    // envoyé en public-read et lu via S3_PUBLIC_URL ; tout le reste est servi par lien signé.
    // S3_UPLOAD_URL est l'endpoint S3 (https://s3.<région>.io.cloud.ovh.net) : la signature SigV4
    // lie cet hôte, il doit donc être joignable par les navigateurs.
    public static string S3_UPLOAD_URL => GetEnvVar("S3_UPLOAD_URL", "http://localhost:9000");
    // Base de lecture publique, sans slash final : l'URL publique du compartiment donnée par OVH
    // (https://<bucket>.s3.<région>.io.cloud.ovh.net) ou un CDN devant lui.
    public static string S3_PUBLIC_URL => GetEnvVar("S3_PUBLIC_URL", "http://localhost:9000/bonprof");
    public static string S3_ACCESS_KEY => GetEnvVar("S3_ACCESS_KEY", "minioadmin");
    public static string S3_SECRET_KEY => GetEnvVar("S3_SECRET_KEY", "minioadmin");
    // Région de signature SigV4, déduite de l'endpoint OVH (s3.gra.io.cloud.ovh.net → gra) ;
    // us-east-1 pour tout autre hôte.
    public static string S3Region
    {
        get
        {
            var host = new Uri(S3_UPLOAD_URL).Host;
            return host.StartsWith("s3.") && host.EndsWith(".cloud.ovh.net")
                ? host.Split('.')[1]
                : "us-east-1";
        }
    }
    public static string S3_BUCKET => GetEnvVar("S3_BUCKET", "bonprof");

    public static int MAX_UPLOAD_SIZE_MB => GetEnvVarInt("MAX_UPLOAD_SIZE_MB", 25);
    public static long MaxUploadSizeBytes => MAX_UPLOAD_SIZE_MB * 1024L * 1024L;

    public static string ALLOWED_EXTENSIONS =>
        GetEnvVar("ALLOWED_EXTENSIONS", ".jpg,.jpeg,.png,.webp,.gif,.pdf,.docx,.xlsx,.csv,.txt,.mp4");

    // Durée de vie des liens signés (factures, pièces justificatives ; les avatars sont publics).
    public static int SIGNED_URL_MINUTES => GetEnvVarInt("SIGNED_URL_MINUTES", 60);

    // Délai avant suppression du fichier d'un justificatif prof remplacé ou refusé (RGPD,
    // minimisation). Doit rester aligné avec la politique de confidentialité côté front.
    public static int PRIVACY_DOCUMENT_RETENTION_DAYS =>
        GetEnvVarInt("PRIVACY_DOCUMENT_RETENTION_DAYS", 30);



}
