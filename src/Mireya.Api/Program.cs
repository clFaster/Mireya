using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Services;
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
builder.Services.AddEndpointsApiExplorer();

// Add NSwag OpenAPI document generation
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "v1";
    config.Title = "Mireya Digital Signage API";
    config.Version = "v1";
});

builder.Services.AddMireyaDbContext(config);

// Add Identity with API endpoints (supports both Bearer tokens and Cookies)
builder.Services.AddIdentityApiEndpoints<User>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    
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

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Register admin user initializer service
builder.Services.AddScoped<IInitializerService, InitializerService>();

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

app.UseAuthentication();
app.UseAuthorization();

// Map Identity API endpoints
app.MapIdentityApi<User>();

app.MapControllers();

app.Run();
