using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Mireya.Database.Models;

namespace Mireya.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<User> userManager,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("reset-admin-password")]
    public async Task<IActionResult> ResetAdminPassword([FromBody] ResetAdminPasswordRequest request)
    {
        // Validate reset token from configuration
        var resetToken = _configuration.GetValue<string>("AdminPasswordResetToken");

        if (string.IsNullOrEmpty(resetToken) || request.ResetToken != resetToken)
        {
            _logger.LogWarning("Invalid admin password reset token attempt");
            return Unauthorized(new { message = "Invalid reset token" });
        }

        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        var adminUser = adminUsers.FirstOrDefault();
        
        if (adminUser == null)
        {
            _logger.LogError("No admin user found for password reset");
            return NotFound(new { message = "Admin user not found" });
        }

        // Remove old password and set new one
        var removePasswordResult = await _userManager.RemovePasswordAsync(adminUser);
        if (!removePasswordResult.Succeeded)
        {
            _logger.LogError("Failed to remove old password");
            return BadRequest(new { message = "Failed to reset password" });
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(adminUser, request.NewPassword);
        if (!addPasswordResult.Succeeded)
        {
            _logger.LogError("Failed to set new password");
            return BadRequest(new { message = "Failed to reset password", errors = addPasswordResult.Errors });
        }

        _logger.LogInformation("Admin password reset successfully for user: {Email}", adminUser.Email);
        return Ok(new { message = "Admin password reset successfully" });
    }
}

public class ResetAdminPasswordRequest
{
    public required string ResetToken { get; set; }
    public required string NewPassword { get; set; }
}
