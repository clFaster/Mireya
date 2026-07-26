using Carter;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Mireya.Api;
using Mireya.Api.Components;
using Mireya.Api.Endpoints;
using Mireya.Api.Extensions;
using Mireya.Api.Hubs;
using Mireya.Api.Middleware;
using Mireya.Api.Services;
using Mireya.Api.Startup;
using Mireya.Application.Constants;
using Mireya.Application.Hubs;
using Mireya.Application.Services;
using Mireya.Application.Services.Alerting;
using Mireya.Application.Services.Asset;
using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.Audit;
using Mireya.Application.Services.Campaign;
using Mireya.Application.Services.Reporting;
using Mireya.Application.Services.ScreenManagement;
using Mireya.Application.Services.Zones;
using Mireya.Database;
using Mireya.Database.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
var config = builder
    .Configuration.AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddUserSecrets<Program>(true, true)
    .AddEnvironmentVariables()
    .Build();

// ─── Blazor Server ───────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

// ─── Carter (REST API) ────────────────────────────────────────────────────────
builder.Services.AddCarter();
builder.Services.AddEndpointsApiExplorer();

// ─── Consistent error responses (RFC 7807 ProblemDetails) ─────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ─── Readiness health check (database connectivity) ───────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// ─── NSwag OpenAPI ───────────────────────────────────────────────────────────
builder.Services.AddOpenApiDocument(generatorSettings =>
{
    generatorSettings.DocumentName = "v1";
    generatorSettings.Title = "Mireya Digital Signage API";
    generatorSettings.Version = "v1";
    generatorSettings.SchemaSettings.SchemaProcessors.Add(new FormFileSchemaProcessor());
});

// ─── Database ────────────────────────────────────────────────────────────────
builder.Services.AddMireyaDbContext(config);

// ─── Identity + dual auth (Bearer for screens/API, Cookie for Blazor admin) ──
builder.Services
    .AddIdentityApiEndpoints<User>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 9;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MireyaDbContext>()
    .AddDefaultTokenProviders();

// Cookie settings for Blazor admin UI
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
});

// AddIdentityApiEndpoints sets Bearer as the default challenge scheme (returns 401).
// Override it so browser-facing Blazor pages redirect to /login instead.
builder.Services.PostConfigure<AuthenticationOptions>(options =>
{
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
});

// ─── Authorization ────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy(Roles.Screen, policy => policy.RequireRole(Roles.Screen));
});

// ─── Application services ─────────────────────────────────────────────────────
builder.Services.AddSignalR(options => { options.EnableDetailedErrors = builder.Environment.IsDevelopment(); });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IInitializerService, InitializerService>();
builder.Services.AddScoped<Mireya.Api.Services.ToastService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IAssetSyncService, AssetSyncService>();
builder.Services.AddSingleton<IScreenConnectionTracker, ScreenConnectionTracker>();
builder.Services.AddScoped<IScreenManagementService, ScreenManagementService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IZoneService, ZoneService>();
builder.Services.AddScoped<IScreenSynchronizationService, ScreenSynchronizationService>();
builder.Services.AddScoped<IPlaybackReportingService, PlaybackReportingService>();
builder.Services.Configure<AlertingOptions>(builder.Configuration.GetSection(AlertingOptions.SectionName));
builder.Services.AddHttpClient(nameof(ScreenAlertService));
builder.Services.AddScoped<IScreenAlertService, ScreenAlertService>();
builder.Services.AddHostedService<ScreenOfflineMonitorService>();
builder.Services.AddHostedService<CampaignScheduleSyncService>();
builder.Services.AddScoped<IScreenHubContext, ScreenHubContextAdapter>();

// ─── CORS (dev only) ──────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

app.UseExceptionHandler();

app.MapDefaultEndpoints();

// ─── Startup: migrations + seed ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<MireyaDbContext>();
    await context.Database.MigrateAsync();
    var adminInitializer = services.GetRequiredService<IInitializerService>();
    await adminInitializer.InitializeAsync();
}

// ─── Middleware pipeline ──────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
    app.UseCors("Development");
}

app.UseStaticFiles(); // Blazor static assets (wwwroot)

// Serve uploaded media files from /uploads
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
});

app.UseRouting();

if (app.Environment.IsDevelopment())
    app.UseResponseDebug();

// SignalR WebSocket connections send the access token as a query-string parameter
// (?access_token=...) because HTTP headers cannot be set on WebSocket upgrade requests.
// The Identity Bearer handler only reads from the Authorization header, so we copy
// the query-string token into the header before authentication runs.
app.Use(async (context, next) =>
{
    var accessToken = context.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(accessToken)
        && context.Request.Path.StartsWithSegments("/hubs"))
    {
        context.Request.Headers.Authorization = $"Bearer {accessToken}";
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapIdentityApi<User>();
app.MapIdentityApiAdditionalEndpoints<User>();

app.MapGroup("/auth").MapLoginEndpoints(); // Cookie login/logout for Blazor admin

app.MapCarter();
app.MapHub<ScreenHub>("/hubs/screen");

// Serve build-time static web assets, including the fingerprinted .NET 10
// Blazor boot script. UseStaticFiles above remains responsible for runtime
// uploads that aren't present in the build manifest.
app.MapStaticAssets();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync();
