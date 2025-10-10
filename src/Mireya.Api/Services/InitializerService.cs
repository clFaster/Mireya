using Microsoft.AspNetCore.Identity;
using Mireya.Database.Models;

namespace Mireya.Api.Services;

public class InitializerService(
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<InitializerService> logger,
    IConfiguration config)
    : IInitializerService
{
    private const string AdminRoleName = "Admin";
    private const string DefaultAdminEmail = "admin@mireya.local";

    public async Task InitializeAsync()
    {
        await EnsureAdminRoleExistsAsync();
        await EnsureDefaultAdminUserExistsAsync();
    }

    private async Task EnsureAdminRoleExistsAsync()
    {
        if (!await roleManager.RoleExistsAsync(AdminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
            logger.LogInformation("Admin role created");
        }
        else
        {
            logger.LogInformation("Admin role already exists");
        }
    }

    private async Task EnsureDefaultAdminUserExistsAsync()
    {
        var (email, password) = GetAdminConfiguration();

        if (string.IsNullOrEmpty(password))
        {
            logger.LogWarning("Default admin password not configured in appsettings. Admin user will not be created");
            return;
        }

        logger.LogInformation("Checking for default admin user with email: {Email}", email);

        var existingUser = await userManager.FindByEmailAsync(email);
        
        if (existingUser == null)
        {
            await CreateAdminUserAsync(email, password);
        }
        else
        {
            await EnsureUserHasAdminRoleAsync(existingUser, email);
        }
    }

    private (string Email, string? Password) GetAdminConfiguration()
    {
        var adminConfig = config.GetSection("DefaultAdminUser");
        var email = adminConfig["Email"] ?? DefaultAdminEmail;
        var password = adminConfig["Password"];
        return (email, password);
    }

    private async Task CreateAdminUserAsync(string email, string password)
    {
        logger.LogInformation("Creating default admin user...");
        
        var adminUser = new User
        {
            UserName = email,  // Identity API login uses username field with email value
            Email = email,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, password);
        
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, AdminRoleName);
            logger.LogInformation("Default admin user created successfully with email: {Email}", email);
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to create admin user: {Errors}", errors);
        }
    }

    private async Task EnsureUserHasAdminRoleAsync(User user, string email)
    {
        logger.LogInformation("Default admin user already exists with email: {Email}", email);
        
        // Ensure the user is in the Admin role
        if (!await userManager.IsInRoleAsync(user, AdminRoleName))
        {
            await userManager.AddToRoleAsync(user, AdminRoleName);
            logger.LogInformation("Added Admin role to existing user: {Email}", email);
        }
    }
}
