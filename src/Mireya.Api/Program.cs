using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Startup;
using Mireya.Database;
using Mireya.Database.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddUserSecrets<Program>(true, true)
    .AddEnvironmentVariables().Build();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMireyaDbContext(config);

// Add Identity with API endpoints
builder.Services.AddIdentity<User, IdentityRole>(options =>
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
})
.AddEntityFrameworkStores<MireyaDbContext>()
.AddApiEndpoints();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

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
await InitializeDefaultAdminUser(services, config);

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    var db = services.GetRequiredService<MireyaDbContext>();
    await MireyaDbContext.InitializeAsync(db);

    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors("Development");
}

app.UseAuthentication();
app.UseAuthorization();

// Map Identity API endpoints
app.MapIdentityApi<User>();

app.MapControllers();

app.Run();

// Method to initialize default admin user
static async Task InitializeDefaultAdminUser(IServiceProvider services, IConfiguration config)
{
    var userManager = services.GetRequiredService<UserManager<User>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    // Create Admin role if it doesn't exist
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
        logger.LogInformation("Admin role created");
    }

    // Check if any admin user exists
    var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
    
    if (!adminUsers.Any())
    {
        var adminConfig = config.GetSection("DefaultAdminUser");
        var username = adminConfig["Username"] ?? "admin";
        var email = adminConfig["Email"] ?? "admin@mireya.local";
        var password = adminConfig["Password"];

        if (string.IsNullOrEmpty(password))
        {
            logger.LogWarning("Default admin password not configured. Admin user will not be created");
            return;
        }

        var adminUser = new User
        {
            UserName = username,
            Email = email,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, password);
        
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            logger.LogInformation("Default admin user created with email: {Email}", email);
        }
        else
        {
            logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
