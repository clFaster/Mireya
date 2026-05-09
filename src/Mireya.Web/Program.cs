using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mireya.Application.Constants;
using Mireya.Application.Services;
using Mireya.Application.Services.Asset;
using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.Campaign;
using Mireya.Application.Services.ScreenManagement;
using Mireya.Database;
using Mireya.Database.Models;
using Mireya.Web.Endpoints;
using Mireya.Web.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// DB
builder.Services.AddMireyaDbContext(builder.Configuration);

// Identity with cookie auth for the admin UI
builder.Services
    .AddIdentity<User, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 9;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MireyaDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Application services (in-process)
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IAssetSyncService, AssetSyncService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IScreenManagementService, ScreenManagementService>();
builder.Services.AddScoped<IInitializerService, InitializerService>();
builder.Services.AddSingleton<IScreenConnectionTracker, ScreenConnectionTracker>();
builder.Services.AddScoped<Mireya.Application.Hubs.IScreenHubContext, Mireya.Web.Services.NoOpScreenHubContext>();
builder.Services.AddScoped<IScreenSynchronizationService, ScreenSynchronizationService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Run migrations and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MireyaDbContext>();
    await db.Database.MigrateAsync();
    var initializer = scope.ServiceProvider.GetRequiredService<IInitializerService>();
    await initializer.InitializeAsync();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGroup("/auth").MapLoginEndpoints();

app.MapRazorComponents<Mireya.Web.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
