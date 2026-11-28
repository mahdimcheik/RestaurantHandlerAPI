using RestaurantHandler.Integrations.EInvoicing;
using RestaurantHandler.Integrations.SumUp;
using System.Text;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.S3;
using RestaurantHandler.Contexts;
using RestaurantHandler.Controllers;
using RestaurantHandler.Infrastructure.Errors;
using RestaurantHandler.Infrastructure.RateLimiting;
using RestaurantHandler.Integrations.Storage;
using RestaurantHandler.Models;
using RestaurantHandler.Persistence.Seed;
using RestaurantHandler.Services;
using RestaurantHandler.Utilities;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Local dev only: .env is gitignored and never shipped in the Docker image, so in
// containers/CI this is a no-op and real environment variables are used instead.
// NoClobber so a real env var (e.g. set by the container) always wins over .env.
var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envPath))
{
    DotNetEnv.Env.NoClobber().Load(envPath);
}

EnvironmentVariables.ValidateRequired();


// Configurer les services
var services = builder.Services;
ConfigureServices(services);

var app = builder.Build();

// Par défaut seuls les proxys en loopback sont crus ; le reverse proxy joint l'API par un
// réseau Docker. L'API ne publie aucun port, donc tout appel vient d'une adresse privée.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
foreach (var (prefix, length) in new[] { ("10.0.0.0", 8), ("172.16.0.0", 12), ("192.168.0.0", 16), ("127.0.0.0", 8) })
{
    forwardedHeaders.KnownIPNetworks.Add(
        new System.Net.IPNetwork(System.Net.IPAddress.Parse(prefix), length)
    );
}
forwardedHeaders.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
app.UseForwardedHeaders(forwardedHeaders);

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MainContext>();
    context.Database.Migrate();

    // Seed default users
    AdminAccountsSeeder.Seed(scope.ServiceProvider);
}

// Réconciliation SumUp : notifications perdues et commandes non payées dans le délai.
if (EnvironmentVariables.SumUpEnabled)
{
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OnlinePaymentService>(
        "sumup-reconcile",
        s => s.ReconcileAllAsync(),
        EnvironmentVariables.SUMUP_RECONCILE_CRON
    );
}

// Facturation électronique : transmission des demandes en attente, si une plateforme est branchée.
if (EnvironmentVariables.EINVOICING_PROVIDER != NoEInvoicingGateway.Name)
{
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<EInvoicingService>(
        "einvoicing-submit",
        s => s.SubmitPendingAsync(),
        EnvironmentVariables.EINVOICING_SUBMIT_CRON
    );
}

// Configurer le pipeline de middleware

ConfigureMiddlewarePipeline(app);

app.Run();

#region Services
static void ConfigureServices(IServiceCollection services)
{
    // Connexion Google : validation de l'ID token (Google.Apis.Auth gère ses propres
    // appels réseau pour les certificats) et récupération de la photo de profil.
    services.AddHttpClient<GoogleAuthService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(5);
    });

    // add services
    services.AddTransient<TokenService>();
    services.AddTransient<RegistrationService>();
    services.AddTransient<SignInService>();
    services.AddTransient<PasswordService>();
    services.AddTransient<AvatarService>();
    services.AddTransient<MailService>();
    services.AddTransient<ProfileService>();
    services.AddTransient<UserAdministrationService>();
    services.AddTransient<UserManagementService>();
    services.AddScoped<CallerResolver>();
    services.AddTransient<RolesService>();
    services.AddTransient<NotificationService>();
    services.AddTransient<NotificationDispatcher>();
    services.AddTransient<PushNotificationService>();
    services.AddSingleton<IWebPushSender, VapidWebPushSender>();

    if (EnvironmentVariables.UsesLocalStorage)
    {
        services.AddSingleton<IObjectStore>(sp => new LocalObjectStore(
            string.IsNullOrWhiteSpace(EnvironmentVariables.STORAGE_LOCAL_PATH)
                ? Path.Combine(sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "wwwroot", "uploads")
                : EnvironmentVariables.STORAGE_LOCAL_PATH,
            string.IsNullOrWhiteSpace(EnvironmentVariables.STORAGE_PUBLIC_URL)
                ? $"{EnvironmentVariables.API_BACK_URL?.TrimEnd('/')}{LocalObjectStore.RequestPath}"
                : EnvironmentVariables.STORAGE_PUBLIC_URL
        ));
    }
    else
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(
                EnvironmentVariables.S3_ACCESS_KEY,
                EnvironmentVariables.S3_SECRET_KEY
            ),
            new AmazonS3Config
            {
                ServiceURL = EnvironmentVariables.S3_UPLOAD_URL,
                AuthenticationRegion = EnvironmentVariables.S3Region,
                ForcePathStyle = true,
                // Presigned URLs take their scheme from this flag, not from ServiceURL: leaving it
                // false hands out https:// links for a plaintext local endpoint.
                UseHttp = EnvironmentVariables.S3_UPLOAD_URL.StartsWith(
                    "http://",
                    StringComparison.OrdinalIgnoreCase
                ),
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            }
        ));
        services.AddSingleton<IObjectStore, S3ObjectStore>();
    }
    services.AddSingleton<StorageService>();

    // Cache mémoire seul (une instance). Brancher Redis en L2 = enregistrer un
    // IDistributedCache (AddStackExchangeRedisCache), sans toucher aux services.
    services.AddHybridCache();

    services.AddSignalR();
    services.AddTransient<RealtimeService>();

    // Carte et restaurant (J1)
    services.AddSingleton(TimeProvider.System);
    services.AddScoped<CatalogChangeNotifier>();
    services.AddScoped<CatalogImageService>();
    services.AddScoped<CategoryService>();
    services.AddScoped<ProductService>();
    services.AddScoped<OptionGroupService>();
    services.AddScoped<IngredientService>();
    services.AddScoped<MenuService>();
    services.AddScoped<PublicCatalogService>();
    services.AddScoped<RestaurantService>();

    // Commandes (J2)
    services.AddScoped<CartService>();
    services.AddScoped<OrderNotifier>();
    services.AddScoped<OrderQueryService>();
    services.AddScoped<OrderPlacementService>();
    services.AddScoped<OrderWorkflowService>();

    // Paiement en ligne SumUp (J3)
    services.AddHttpClient<SumUpClient>(SumUpClient.Configure);
    services.AddScoped<OnlinePaymentService>();

    // Facturation électronique (ADR-0014) : une seule plateforme active, choisie par EINVOICING_PROVIDER.
    services.AddScoped<BusinessProfileService>();
    services.AddScoped<EInvoicingService>();
    switch (EnvironmentVariables.EINVOICING_PROVIDER)
    {
        case NoEInvoicingGateway.Name:
            services.AddSingleton<IEInvoicingGateway, NoEInvoicingGateway>();
            break;
        default:
            throw new InvalidOperationException(
                $"EINVOICING_PROVIDER inconnu : « {EnvironmentVariables.EINVOICING_PROVIDER} »."
            );
    }

    // Statistiques (J5)
    services.AddScoped<StatsService>();

    services.AddLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddConsole();
        loggingBuilder.AddDebug();
        // Hide the SQL queries EF Core logs at Information level, keep warnings/errors
        loggingBuilder.AddFilter(
            "Microsoft.EntityFrameworkCore.Database.Command",
            EnvironmentVariables.LOG_SQL ? LogLevel.Information : LogLevel.Warning
        );
    });

    services.AddRouting(opt => opt.LowercaseUrls = true);

    var configuration = services.BuildServiceProvider().GetService<IConfiguration>();

    // db
    var connString =
        $"Host={EnvironmentVariables.DB_HOST};"
        + $"Port={EnvironmentVariables.DB_PORT};"
        + $"Database={EnvironmentVariables.DB_NAME};"
        + $"Username={EnvironmentVariables.DB_USER};"
        + $"Password={EnvironmentVariables.DB_PASSWORD};";

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connString);
    dataSourceBuilder.EnableDynamicJson();
    var dataSource = dataSourceBuilder.Build();

    services.AddDbContext<MainContext>(options =>
    {
        options.UseNpgsql(
            dataSource,
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        );
    });

    services.Configure<DataProtectionTokenProviderOptions>(options =>
    {
        options.TokenLifespan = TimeSpan.FromHours(1);
    });

    ConfigureCors(services);
    ConfigureControllers(services);
    ConfigureSwagger(services);
    ConfigureIdentity(services);
    ConfigureAuthentication(services);
    ConfigureAuthorization(services);
    ConfigureHangFire(services);
    services.AddAppRateLimiting();
}

static void ConfigureCors(IServiceCollection services)
{
    services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder
                .SetIsOriginAllowed(CorsHelper.IsOriginAllowed)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders("filename", "Content-Disposition");
        });
    });
}

static void ConfigureControllers(IServiceCollection services)
{
    services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); // pour que les enums soient en string dans les JSON, sinon le batard ng-openapi les met en int
        });
    ;
    services.AddEndpointsApiExplorer();
    services.AddProblemDetails();
    services.AddExceptionHandler<BusinessExceptionHandler>();
}

static void ConfigureIdentity(IServiceCollection services)
{
    services
        .AddIdentity<UserApp, RoleApp>()
        .AddEntityFrameworkStores<MainContext>()
        .AddRoleManager<RoleManager<RoleApp>>()
        .AddUserManager<UserManager<UserApp>>()
        .AddSignInManager<SignInManager<UserApp>>()
        .AddDefaultTokenProviders()
        .AddTokenProvider<EmailConfirmationTokenProvider<UserApp>>(
            EmailConfirmationTokenProvider<UserApp>.ProviderName
        );

    services.Configure<IdentityOptions>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(2);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.AllowedUserNameCharacters =
            " abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.User.RequireUniqueEmail = true;

        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;

        options.Tokens.EmailConfirmationTokenProvider =
            EmailConfirmationTokenProvider<UserApp>.ProviderName;
    });
}

static void ConfigureAuthentication(IServiceCollection services)
{
    services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = EnvironmentVariables.API_BACK_URL,
                ValidAudience = EnvironmentVariables.API_BACK_URL,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(EnvironmentVariables.JWT_KEY)
                ),
            };
            // Un navigateur ne peut pas poser d'en-tête Authorization sur un WebSocket : SignalR
            // transmet le jeton en paramètre de requête, accepté sur le seul chemin des hubs.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    if (
                        !string.IsNullOrEmpty(token)
                        && context.HttpContext.Request.Path.StartsWithSegments("/hubs")
                    )
                    {
                        context.Token = token;
                    }
                    return Task.CompletedTask;
                },
            };
        });
}

static void ConfigureAuthorization(IServiceCollection services)
{
    // Refus par défaut : une opération sans déclaration explicite exige une session.
    // Les opérations publiques portent [AllowAnonymous].
    services.AddAuthorization(options =>
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
    );
}

static void ConfigureHangFire(IServiceCollection services)
{
    // Hangfire configuration can be added here if needed in the future
    services.AddHangfire(configuration =>
        configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            // Les jobs persistés avant le renommage BonProf → RestaurantHandler portent
            // encore l'ancien namespace / nom d'assembly : on les traduit à la lecture.
            .UseTypeResolver(typeName =>
                Hangfire.Common.TypeHelper.DefaultTypeResolver(
                    System.Text.RegularExpressions.Regex.Replace(
                        typeName,
                        @"\bBonProf\b",
                        "RestaurantHandler"
                    )
                )
            )
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                (options) =>
                {
                    options.UseNpgsqlConnection(
                        $"Host={EnvironmentVariables.DB_HOST};Port={EnvironmentVariables.DB_PORT};Database={EnvironmentVariables.DB_NAME};Username={EnvironmentVariables.DB_USER};Password={EnvironmentVariables.DB_PASSWORD};CommandTimeout=60;"
                    );
                }
            )
    );

    services.AddHangfireServer();
}

static void ConfigureSwagger(IServiceCollection services)
{
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "RestaurantHandler API",
                Version = "v1",
                Description = "API de commande du restaurant",
            }
        );

        var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (System.IO.File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }

        c.DocumentFilter<SharedSchemasDocumentFilter>();

        c.AddSecurityDefinition(
            "Bearer",
            new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
            }
        );

        c.AddSecurityRequirement(
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    new string[] { }
                },
            }
        );
    });
}

#endregion

#region MiddleWare
static void ConfigureMiddlewarePipeline(WebApplication app)
{
    // En tête : toute exception des étapes suivantes devient une réponse ProblemDetails.
    app.UseExceptionHandler();

    var supportedCultures = new string[] { "fr-FR" };
    app.UseRequestLocalization(options =>
        options
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures)
            .SetDefaultCulture("fr-FR")
    );

    // Add payload size limit middleware early in the pipeline
    app.Use(
        async (context, next) =>
        {
            if (context.Request.ContentLength > 200_000_000)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await context.Response.WriteAsync("Payload Too Large");
                return;
            }

            await next.Invoke();
        }
    );

    // Mode local : fichiers envoyés, tous publics, clés à GUID jamais réécrites (cache illimité).
    // Avant le UseStaticFiles par défaut, qui servirait aussi wwwroot/uploads sans ces en-têtes.
    if (app.Services.GetRequiredService<IObjectStore>() is LocalObjectStore localStore)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(localStore.Root),
            RequestPath = LocalObjectStore.RequestPath,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
            },
        });
    }

    app.UseStaticFiles();

    app.UseSwagger();
    if (
        app.Environment.IsDevelopment()
        || string.Equals(
            Environment.GetEnvironmentVariable("ALLOW_DEMO_SEED"),
            "true",
            StringComparison.OrdinalIgnoreCase
        )
    )
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "RestaurantHandler API v1");
            c.RoutePrefix = string.Empty;
            c.DocumentTitle = "RestaurantHandler API";
        });
    }

    app.UseRouting();

    app.UseCors();

    app.UseAuthentication();
    app.UseHangfireDashboard(
        "/hangfire",
        new DashboardOptions
        {
            Authorization =
            [
                new HangfireAdminAuthorizationFilter(
                    app.Environment.IsDevelopment() && EnvironmentVariables.HANGFIRE_DASHBOARD_ALLOW_LOCAL
                ),
            ],
        }
    );

    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHub<RealtimeHub>(RealtimeHub.Path);

    // Version gravée dans l'image au build (ARG/ENV APP_VERSION du Dockerfile).
    // Le client peut comparer sa version à celle-ci.
    app.MapGet(
            "/version",
            () =>
                Results.Ok(
                    new { version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev" }
                )
        )
        .AllowAnonymous();
}
#endregion

public partial class Program;
