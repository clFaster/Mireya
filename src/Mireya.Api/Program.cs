using Carter;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Mireya.Api;
using Mireya.Api.Components;
using Mireya.Api.Endpoints;
using Mireya.Api.Extensions;
using Mireya.Api.Hubs;
using Mireya.Api.Middleware;
using Mireya.Api.Startup;
using Mireya.Application.Constants;
using Mireya.Application.Hubs;
using Mireya.Application.Services;
using Mireya.Application.Services.Asset;
using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.Campaign;
using Mireya.Application.Services.ScreenManagement;
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

// ─── Authorization ────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy(Roles.Screen, policy => policy.RequireRole(Roles.Screen));
});

// ─── Application services ─────────────────────────────────────────────────────
builder.Services.AddSignalR(options => { options.EnableDetailedErrors = true; });
builder.Services.AddScoped<IInitializerService, InitializerService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IAssetSyncService, AssetSyncService>();
builder.Services.AddSingleton<IScreenConnectionTracker, ScreenConnectionTracker>();
builder.Services.AddScoped<IScreenManagementService, ScreenManagementService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IScreenSynchronizationService, ScreenSynchronizationService>();
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

app.MapDefaultEndpoints();

// ─── Startup: migrations + seed ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<MireyaDbContext>();
    await context.Database.MigrateAsync();
    var adminInitializer = services.GetRequiredService<IInitializerService>();
    await adminInitializer.InitializeAsync();

    if (app.Environment.IsDevelopment())
    {
        var db = services.GetRequiredService<MireyaDbContext>();
        await MireyaDbContext.InitializeAsync(db);
    }
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
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "uploads"));
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "uploads")),
    RequestPath = "/uploads",
});

app.UseRouting();
app.UseResponseDebug();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapIdentityApi<User>();
app.MapIdentityApiAdditionalEndpoints<User>();

app.MapGroup("/auth").MapLoginEndpoints(); // Cookie login/logout for Blazor admin

app.MapCarter();
app.MapHub<ScreenHub>("/hubs/screen");

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

await app.RunAsync();
