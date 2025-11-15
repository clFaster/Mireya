using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Mireya.Api;
using Mireya.Api.Constants;
using Mireya.Api.Extensions;
using Mireya.Api.Middleware;
using Mireya.Api.Services;
using Mireya.Api.Services.Asset;
using Mireya.Api.Services.Campaign;
using Mireya.Api.Services.ScreenManagement;
using Mireya.Api.Startup;
using Mireya.Database;
using Mireya.Database.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddUserSecrets<Program>(true, true)
    .AddEnvironmentVariables().Build();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddRazorPages(options =>
{
    // Require authentication for all pages in the Admin area by default
    options.Conventions
        .AuthorizeAreaFolder("Admin", "/", Roles.Admin)
        .AllowAnonymousToAreaPage("Admin", "/Login");
});
builder.Services.AddEndpointsApiExplorer();

// Add NSwag OpenAPI document generation
builder.Services.AddOpenApiDocument(generatorSettings =>
{
    generatorSettings.DocumentName = "v1";
    generatorSettings.Title = "Mireya Digital Signage API";
    generatorSettings.Version = "v1";
    
    // Process IFormFile as binary string for file uploads
    generatorSettings.SchemaSettings.SchemaProcessors.Add(new FormFileSchemaProcessor());
});

builder.Services.AddMireyaDbContext(config);

// Add Identity with API endpoints (supports both Bearer tokens and Cookies)
builder.Services.AddIdentityApiEndpoints<User>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 9;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // User settings
    options.User.RequireUniqueEmail = true;
    
    // SignIn settings - allow signing in with email
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<MireyaDbContext>()
.AddDefaultTokenProviders();

// Add default authentication scheme (cookies) for Razor Pages
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
});

builder.Services.AddAuthorization(options =>
{
    // Add policy for Admin role
    options.AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin));
});

// Configure cookie authentication to redirect to login page on unauthorized access
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
});

// Register services
builder.Services.AddScoped<IInitializerService, InitializerService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IScreenManagementService, ScreenManagementService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
var context = services.GetRequiredService<MireyaDbContext>();

// Apply pending migrations automatically
await context.Database.MigrateAsync();

// Initialize default admin user
var adminInitializer = services.GetRequiredService<IInitializerService>();
await adminInitializer.InitializeAsync();

// Configure the HTTP request pipeline.
// Only use HTTPS redirection in production to avoid 307 redirects in development
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    var db = services.GetRequiredService<MireyaDbContext>();
    await MireyaDbContext.InitializeAsync(db);
    
    // Enable NSwag middleware for document generation and Swagger UI
    app.UseOpenApi();
    app.UseSwaggerUi();
    
    app.UseCors("Development");
}

// Establish routing context (required for authentication/authorization to work with Razor Pages)
app.UseRouting();

// Add response debug middleware (logs unauthorized and error responses)
app.UseResponseDebug();

app.UseAuthentication();
app.UseAuthorization();

// Serve static files for Razor Pages (CSS, JS, images)
app.UseStaticFiles();

// Serve uploaded files
// Erstelle das Verzeichnis "uploads", falls es nicht existiert
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "uploads"));
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "uploads")),
    RequestPath = "/uploads"
});

// Map Identity API endpoints
app.MapIdentityApi<User>();
app.MapIdentityApiAdditionalEndpoints<User>();

// Map Controllers and Razor Pages
app.MapControllers();
app.MapRazorPages();

// Root page is handled by Pages/Index.cshtml (no redirect needed)

await app.RunAsync();
