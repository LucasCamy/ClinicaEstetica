using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PainelEstetica.Application.Appointments;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Catalog;
using PainelEstetica.Application.Clients;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Content;
using PainelEstetica.Application.Identity;
using PainelEstetica.Application.Integrations;
using PainelEstetica.Application.Leads;
using PainelEstetica.Application.Forms;
using PainelEstetica.Application.Finance;
using PainelEstetica.Application.Reports;
using PainelEstetica.Application.Security;
using PainelEstetica.Application.Settings;
using PainelEstetica.Application.Terms;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Data.Migrations;
using PainelEstetica.Infrastructure.Identity;
using PainelEstetica.Infrastructure.Services;
using PainelEstetica.WebAPI.Endpoints;
using PainelEstetica.WebAPI.Middlewares;
using PainelEstetica.WebAPI.Security;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Geração 100% local de PDFs clínicos. A licença Community requer elegibilidade
// contratual; altere Pdf:QuestPdfLicense se a clínica utilizar licença comercial.
QuestPDF.Settings.License = builder.Configuration["Pdf:QuestPdfLicense"]?.Trim().ToLowerInvariant() switch
{
    "professional" => LicenseType.Professional,
    "enterprise" => LicenseType.Enterprise,
    _ => LicenseType.Community
};

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringTrimConverter());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Mantém acentos legíveis no JSON sem liberar caracteres sensíveis a HTML.
    options.SerializerOptions.Encoder = JavaScriptEncoder.Create(
        UnicodeRanges.BasicLatin,
        UnicodeRanges.Latin1Supplement,
        UnicodeRanges.LatinExtendedA);
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PainelEstetica API", Version = "v1" });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .WithHeaders("Content-Type", "X-CSRF-TOKEN")
                .AllowCredentials();
        });
    });
}

var clinicOptions = builder.Configuration
    .GetSection(ClinicOptions.SectionName)
    .Get<ClinicOptions>() ?? new ClinicOptions();
builder.Services.AddSingleton(clinicOptions);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IClinicClock, ClinicClock>();
builder.Services.AddSingleton<StorageQuotaManager>();

var useInMemory = builder.Configuration.GetValue<bool>("Database:UseInMemory");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("DATABASE_URL");
if (useInMemory)
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("Banco em memória não pode ser utilizado em produção.");
    }

    builder.Services.AddDbContext<EsteticaDbContext>(options =>
        options.UseInMemoryDatabase(
            builder.Configuration["Database:InMemoryName"]
            ?? $"PainelEstetica-{builder.Environment.EnvironmentName}"));
}
else if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<EsteticaDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly(typeof(EsteticaDbContext).Assembly.FullName)));
}
else
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection é obrigatória quando Database:UseInMemory não está habilitado.");
}

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
    })
    .AddEntityFrameworkStores<EsteticaDbContext>()
    .AddClaimsPrincipalFactory<ApplicationClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IdentityErrorDescriber, PortugueseIdentityErrorDescriber>();

var isDevelopment = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var allowInsecureHttpCookies = builder.Configuration.GetValue<bool>("Security:AllowInsecureHttpCookies");
var useDevelopmentCookies = isDevelopment || allowInsecureHttpCookies;
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = useDevelopmentCookies ? "Clinica.Session.Local" : "__Host-Clinica.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = useDevelopmentCookies ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
    // A profissional usa o painel durante atendimentos completos. A sessão dura duas horas
    // e é renovada enquanto houver atividade, sem sacrificar o logout após inatividade.
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(2));
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromMinutes(30));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = useDevelopmentCookies ? "Clinica.Antiforgery.Local" : "__Host-Clinica.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = useDevelopmentCookies ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
});

var configuredKeysPath = builder.Configuration["DataProtection:KeysPath"] ?? "data-protection-keys";
var keysPath = Path.IsPathRooted(configuredKeysPath)
    ? configuredKeysPath
    : Path.Combine(AppContext.BaseDirectory, configuredKeysPath);
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("PainelEstetica");

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in AppPermissions.All)
    {
        options.AddPolicy(permission, policy => policy.AddRequirements(new PermissionRequirement(permission)));
    }
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IFormSubmissionService, FormSubmissionService>();
builder.Services.AddScoped<IFormSubmissionPdfArchiver, FormSubmissionPdfArchiver>();
builder.Services.AddScoped<ITermService, TermService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IProcedureService, ProcedureService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IUserAccessService, UserAccessService>();
builder.Services.AddScoped<IClinicSettingsService, ClinicSettingsService>();
builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));
builder.Services.AddHttpClient("GoogleCalendar");
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:AuthPermitLimit") ?? 5;
    options.AddPolicy("auth", context => FixedWindow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", authPermitLimit, TimeSpan.FromMinutes(1)));
    options.AddPolicy("public-leads", context => FixedWindow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", 5, TimeSpan.FromMinutes(10)));
    options.AddPolicy("uploads", context => FixedWindow(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ??
        "unknown",
        20,
        TimeSpan.FromMinutes(10)));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});
builder.Services.AddHealthChecks().AddDbContextCheck<EsteticaDbContext>();

var app = builder.Build();

if (allowInsecureHttpCookies && app.Environment.IsProduction())
{
    app.Logger.LogWarning(
        "Cookies HTTP locais estão habilitados. Use esta opção somente em loopback e desabilite-a antes de publicar pelo Cloudflare Tunnel.");
}

app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
        context.Response.Headers.TryAdd("Cache-Control", "no-store");
        return Task.CompletedTask;
    });
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "PainelEstetica API v1"));
}

if (allowedOrigins.Length > 0) app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

await InitializeDatabaseAsync(app, useInMemory);

app.MapHealthChecks("/health");
app.MapEsteticaEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "PainelEstetica API", status = "healthy" }));
app.Run();

static RateLimitPartition<string> FixedWindow(string key, int permits, TimeSpan window) =>
    RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permits,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true
    });

static async Task InitializeDatabaseAsync(WebApplication app, bool useInMemory)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<EsteticaDbContext>();
    if (useInMemory)
    {
        await db.Database.EnsureCreatedAsync();
    }
    else if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
    {
        if (app.Environment.IsProduction()
            && !app.Configuration.GetValue<bool>("Database:AllowProductionMigrations"))
        {
            throw new InvalidOperationException(
                "Database:ApplyMigrations em produção requer Database:AllowProductionMigrations=true.");
        }

        const string initialMigration = "20260805185052_InitialSchema";
        if (await LegacyDatabaseAdapter.UpgradeIfRequiredAsync(db, initialMigration))
        {
            app.Logger.LogInformation("Banco legado adaptado com preservação dos registros existentes.");
        }

        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations aplicadas com sucesso durante a inicialização.");
    }

    await BootstrapIdentityAsync(scope.ServiceProvider, app.Configuration, app.Logger);
}

static async Task BootstrapIdentityAsync(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger logger)
{
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var clock = services.GetRequiredService<IClock>();

    foreach (var role in AppRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(error => error.Description)));
            }
        }
    }

    var username = configuration["BootstrapAdmin:Username"]?.Trim();
    var email = configuration["BootstrapAdmin:Email"]?.Trim();
    var password = configuration["BootstrapAdmin:Password"];
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        if (!await userManager.Users.AnyAsync(user => user.IsActive))
        {
            logger.LogWarning("Nenhum usuário ativo. Configure BootstrapAdmin por secrets para criar o primeiro administrador.");
        }
        return;
    }

    var admin = await userManager.FindByNameAsync(username);
    if (admin is null)
    {
        admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = clock.UtcNow,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
        logger.LogInformation("Administrador inicial criado; a troca de senha e o MFA serão obrigatórios no primeiro acesso.");
    }

    if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
    {
        var result = await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}

public partial class Program;
