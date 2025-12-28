using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mireya.Api.Constants;
using Mireya.Api.Services.Asset;
using Mireya.Database.Models;

namespace Mireya.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/[controller]")]
public class AssetController(IAssetService assetService) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<List<AssetSummary>>> UploadAssets(
        [FromForm] UploadFilesRequest request
    )
    {
        try
        {
            var result = await assetService.UploadAssetsAsync(request.Files);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedAssets>> GetAssets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] AssetType? type = null,
        [FromQuery] string sortBy = "name"
    )
    {
        var result = await assetService.GetAssetsAsync(page, pageSize, type, sortBy);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsset(Guid id)
    {
        try
        {
            await assetService.DeleteAssetAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/metadata")]
    public async Task<ActionResult<Asset>> UpdateAssetMetadata(
        Guid id,
        [FromBody] UpdateAssetMetadataRequest request
    )
    {
        try
        {
            var asset = await assetService.UpdateAssetMetadataAsync(id, request);
            return Ok(asset);
        }
        catch (ArgumentNullException)
        {
            return BadRequest("Request body is required");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("website")]
    public async Task<ActionResult<AssetSummary>> CreateWebsiteAsset(
        [FromBody] CreateWebsiteAssetRequest request
    )
    {
        try
        {
            var result = await assetService.CreateWebsiteAssetAsync(
                request.Url,
                request.Name,
                request.Description
            );
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
