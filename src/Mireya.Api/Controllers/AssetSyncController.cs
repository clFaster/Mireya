using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Constants;
using Mireya.Api.Services.AssetSync;
using Mireya.Database;
using System.Security.Claims;

namespace Mireya.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetSyncController(IAssetSyncService assetSyncService, MireyaDbContext db) : ControllerBase
{
    [HttpPost("status")]
    [Authorize(Roles = Roles.Screen)]
    public async Task<ActionResult> UpdateSyncStatus([FromBody] UpdateAssetSyncRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found");
        }

        // Get displayId from userId
        var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
        if (display == null)
        {
            return NotFound("Display not found for current user");
        }

        try
        {
            await assetSyncService.UpdateAssetSyncStatusAsync(display.Id, request);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("status")]
    [Authorize(Roles = Roles.Screen)]
    public async Task<ActionResult<List<AssetSyncStatusDto>>> GetSyncStatus()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found");
        }

        // Get displayId from userId
        var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
        if (display == null)
        {
            return NotFound("Display not found for current user");
        }

        try
        {
            var statuses = await assetSyncService.GetSyncStatusForDisplayAsync(display.Id);
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("campaigns")]
    [Authorize(Roles = Roles.Screen)]
    public async Task<ActionResult<List<CampaignSyncInfo>>> GetCampaignsToSync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found");
        }

        // Get displayId from userId
        var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
        if (display == null)
        {
            return NotFound("Display not found for current user");
        }

        try
        {
            var campaigns = await assetSyncService.GetCampaignsToSyncAsync(display.Id);
            return Ok(campaigns);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // Admin endpoint to get sync status for any display
    [HttpGet("{displayId:guid}/status")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<AssetSyncStatusDto>>> GetSyncStatusForDisplay(Guid displayId)
    {
        try
        {
            var statuses = await assetSyncService.GetSyncStatusForDisplayAsync(displayId);
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
