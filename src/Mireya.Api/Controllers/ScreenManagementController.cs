using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mireya.Api.Constants;
using Mireya.Api.Services.ScreenManagement;
using Mireya.Database.Models;

namespace Mireya.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScreenManagementController(IScreenManagementService screenManagementService) : ControllerBase
{
    /// <summary>
    /// Anonymous endpoint for a screen to register itself the first time it connects
    /// </summary>
    /// <param name="request">Registration request containing device information</param>
    /// <returns>Unique token for the screen to use for authentication</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterScreenResponse>> RegisterScreen([FromBody] RegisterScreenRequest request)
    {
        try
        {
            var response = await screenManagementService.RegisterScreenAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Bonjour endpoint for authenticated screens to fetch their data
    /// </summary>
    /// <returns>Screen details for the authenticated user</returns>
    [HttpGet("bonjour")]
    [Authorize(Roles = Roles.Screen)]
    public async Task<ActionResult<BonjourResponse>> Bonjour()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User ID not found in claims" });
            }
            
            var response = await screenManagementService.GetBonjourAsync(userId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a paginated list of all registered screens
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10, max: 100)</param>
    /// <param name="status">Filter by approval status</param>
    /// <param name="sortBy">Sort by field (name, location, status, lastseen, default: created date)</param>
    /// <returns>Paginated list of screens</returns>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedScreensResponse>> GetScreens(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ApprovalStatus? status = null,
        [FromQuery] string? sortBy = null)
    {
        try
        {
            var response = await screenManagementService.GetScreensAsync(page, pageSize, status, sortBy);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get details of a specific screen by ID
    /// </summary>
    /// <param name="id">Screen ID</param>
    /// <returns>Screen details</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ScreenDetailsResponse>> GetScreenById(Guid id)
    {
        try
        {
            var response = await screenManagementService.GetScreenByIdAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Screen with ID {id} not found" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update screen details (name, location, description)
    /// </summary>
    /// <param name="id">Screen ID</param>
    /// <param name="request">Update request with new values</param>
    /// <returns>Updated screen details</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ScreenDetailsResponse>> UpdateScreen(Guid id, [FromBody] UpdateScreenRequest request)
    {
        try
        {
            var response = await screenManagementService.UpdateScreenAsync(id, request);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Screen with ID {id} not found" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve a screen registration and create a user account for it
    /// </summary>
    /// <param name="id">Screen ID</param>
    /// <returns>Updated screen details with approval status</returns>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApproveScreenResponse>> ApproveScreen(Guid id)
    {
        try
        {
            var response = await screenManagementService.ApproveScreenAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Screen with ID {id} not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reject a screen registration
    /// </summary>
    /// <param name="id">Screen ID</param>
    /// <returns>Updated screen details with rejection status</returns>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ScreenDetailsResponse>> RejectScreen(Guid id)
    {
        try
        {
            var response = await screenManagementService.RejectScreenAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Screen with ID {id} not found" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}