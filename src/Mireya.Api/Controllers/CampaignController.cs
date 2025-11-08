using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mireya.Api.Constants;
using Mireya.Api.Services.Campaign;

namespace Mireya.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/[controller]")]
public class CampaignController(ICampaignService campaignService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CampaignSummary>>> GetCampaigns([FromQuery] Guid? displayId = null)
    {
        try
        {
            var campaigns = await campaignService.GetCampaignsAsync(displayId);
            return Ok(campaigns);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDetail>> GetCampaign(Guid id)
    {
        try
        {
            var campaign = await campaignService.GetCampaignAsync(id);
            return Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Campaign with ID {id} not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<CampaignDetail>> CreateCampaign([FromBody] CreateCampaignRequest request)
    {
        try
        {
            var campaign = await campaignService.CreateCampaignAsync(request);
            return CreatedAtAction(nameof(GetCampaign), new { id = campaign.Id }, campaign);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CampaignDetail>> UpdateCampaign(Guid id, [FromBody] UpdateCampaignRequest request)
    {
        try
        {
            var campaign = await campaignService.UpdateCampaignAsync(id, request);
            return Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Campaign with ID {id} not found");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCampaign(Guid id)
    {
        try
        {
            await campaignService.DeleteCampaignAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Campaign with ID {id} not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
