using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Hubs;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Services.Campaign;

public interface ICampaignService
{
    Task<List<CampaignSummary>> GetCampaignsAsync(Guid? displayId = null);
    Task<CampaignDetail> GetCampaignAsync(Guid id);
    Task<CampaignDetail> CreateCampaignAsync(CreateCampaignRequest request);
    Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request);
    Task DeleteCampaignAsync(Guid id);
    Task<List<Guid>> GetCampaignsUsingAssetAsync(Guid assetId);
}

public class CampaignService(MireyaDbContext db, IScreenSynchronizationService syncService) : ICampaignService
{
    private const int DefaultDurationSeconds = 10;

    public async Task<List<CampaignSummary>> GetCampaignsAsync(Guid? displayId = null)
    {
        var query = db.Campaigns
            .Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .AsQueryable();

        if (displayId.HasValue)
        {
            query = query.Where(c => c.CampaignAssignments.Any(ca => ca.DisplayId == displayId.Value));
        }

        var campaigns = await query
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

        return campaigns.Select(c => new CampaignSummary(
            c.Id,
            c.Name,
            c.Description,
            c.CampaignAssets.Count,
            c.CampaignAssignments.Count,
            c.CreatedAt,
            c.UpdatedAt
        )).ToList();
    }

    public async Task<CampaignDetail> GetCampaignAsync(Guid id)
    {
        var campaign = await db.Campaigns
            .Include(c => c.CampaignAssets)
                .ThenInclude(ca => ca.Asset)
            .Include(c => c.CampaignAssignments)
                .ThenInclude(ca => ca.Display)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        var assets = campaign.CampaignAssets
            .OrderBy(ca => ca.Position)
            .Select(ca => new CampaignAssetDetail(
                ca.Id,
                ca.AssetId,
                ca.Asset.Name,
                ca.Asset.Type,
                ca.Asset.Source,
                ca.Position,
                ca.DurationSeconds,
                ResolveAssetDuration(ca.Asset, ca.DurationSeconds)
            ))
            .ToList();

        var displays = campaign.CampaignAssignments
            .Select(ca => new DisplayInfo(
                ca.Display.Id,
                ca.Display.Name,
                ca.Display.Location
            ))
            .ToList();

        return new CampaignDetail(
            campaign.Id,
            campaign.Name,
            campaign.Description,
            assets,
            displays,
            campaign.CreatedAt,
            campaign.UpdatedAt
        );
    }

    public async Task<CampaignDetail> CreateCampaignAsync(CreateCampaignRequest request)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Campaign name is required");

        if (request.Assets.Any(a => a.Position <= 0))
            throw new ArgumentException("Asset positions must be positive integers");

        if (request.Assets.Any(a => a.DurationSeconds.HasValue && a.DurationSeconds.Value <= 0))
            throw new ArgumentException("Duration must be positive if provided");

        // Verify assets exist
        var assetIds = request.Assets.Select(a => a.AssetId).Distinct().ToList();
        if (assetIds.Count != request.Assets.Count)
            throw new ArgumentException("Duplicate assets are not allowed in a campaign");

        var existingAssets = await db.Assets
            .Where(a => assetIds.Contains(a.Id))
            .ToListAsync();

        if (existingAssets.Count != assetIds.Count)
        {
            var missingIds = assetIds.Except(existingAssets.Select(a => a.Id)).ToList();
            throw new ArgumentException($"One or more assets do not exist. Missing asset IDs: {string.Join(", ", missingIds)}");
        }

        // Verify displays exist
        if (request.DisplayIds.Any())
        {
            var existingDisplays = await db.Displays
                .Where(d => request.DisplayIds.Contains(d.Id))
                .CountAsync();

            if (existingDisplays != request.DisplayIds.Distinct().Count())
                throw new ArgumentException("One or more displays do not exist");
        }

        // Create campaign
        var campaign = new Database.Models.Campaign
        {
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Campaigns.Add(campaign);

        // Add campaign assets
        foreach (var assetDto in request.Assets)
        {
            var campaignAsset = new CampaignAsset
            {
                CampaignId = campaign.Id,
                AssetId = assetDto.AssetId,
                Position = assetDto.Position,
                DurationSeconds = assetDto.DurationSeconds
            };
            db.CampaignAssets.Add(campaignAsset);
        }

        // Add campaign assignments
        foreach (var displayId in request.DisplayIds)
        {
            var campaignAssignment = new CampaignAssignment
            {
                CampaignId = campaign.Id,
                DisplayId = displayId
            };
            db.CampaignAssignments.Add(campaignAssignment);
        }

        await db.SaveChangesAsync();

        var campaignDetail = await GetCampaignAsync(campaign.Id);
        await syncService.SyncScreensAsync(request.DisplayIds);

        return campaignDetail;
    }

    public async Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Campaign name is required");

        if (request.Assets.Any(a => a.Position <= 0))
            throw new ArgumentException("Asset positions must be positive integers");

        if (request.Assets.Any(a => a.DurationSeconds.HasValue && a.DurationSeconds.Value <= 0))
            throw new ArgumentException("Duration must be positive if provided");

        var campaign = await db.Campaigns
            .Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        var oldDisplayIds = campaign.CampaignAssignments.Select(ca => ca.DisplayId).ToList();

        // Verify assets exist
        var assetIds = request.Assets.Select(a => a.AssetId).Distinct().ToList();
        if (assetIds.Count != request.Assets.Count)
            throw new ArgumentException("Duplicate assets are not allowed in a campaign");

        var existingAssets = await db.Assets
            .Where(a => assetIds.Contains(a.Id))
            .ToListAsync();

        if (existingAssets.Count != assetIds.Count)
        {
            var missingIds = assetIds.Except(existingAssets.Select(a => a.Id)).ToList();
            throw new ArgumentException($"One or more assets do not exist. Missing asset IDs: {string.Join(", ", missingIds)}");
        }

        // Verify displays exist
        if (request.DisplayIds.Any())
        {
            var existingDisplays = await db.Displays
                .Where(d => request.DisplayIds.Contains(d.Id))
                .CountAsync();

            if (existingDisplays != request.DisplayIds.Distinct().Count())
                throw new ArgumentException("One or more displays do not exist");
        }

        // Update campaign properties
        campaign.Name = request.Name;
        campaign.Description = request.Description;
        campaign.UpdatedAt = DateTime.UtcNow;

        // Remove existing assets and add new ones
        db.CampaignAssets.RemoveRange(campaign.CampaignAssets);
        foreach (var assetDto in request.Assets)
        {
            var campaignAsset = new CampaignAsset
            {
                CampaignId = campaign.Id,
                AssetId = assetDto.AssetId,
                Position = assetDto.Position,
                DurationSeconds = assetDto.DurationSeconds
            };
            db.CampaignAssets.Add(campaignAsset);
        }

        // Remove existing assignments and add new ones
        db.CampaignAssignments.RemoveRange(campaign.CampaignAssignments);
        foreach (var displayId in request.DisplayIds)
        {
            var campaignAssignment = new CampaignAssignment
            {
                CampaignId = campaign.Id,
                DisplayId = displayId
            };
            db.CampaignAssignments.Add(campaignAssignment);
        }

        await db.SaveChangesAsync();

        var campaignDetail = await GetCampaignAsync(campaign.Id);
        
        // Notify all affected displays (both new and removed)
        var allAffectedDisplayIds = oldDisplayIds.Union(request.DisplayIds).ToList();
        await syncService.SyncScreensAsync(allAffectedDisplayIds);

        return campaignDetail;
    }

    public async Task DeleteCampaignAsync(Guid id)
    {
        var campaign = await db.Campaigns.FindAsync(id);
        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();
    }

    public async Task<List<Guid>> GetCampaignsUsingAssetAsync(Guid assetId)
    {
        return await db.CampaignAssets
            .Where(ca => ca.AssetId == assetId)
            .Select(ca => ca.CampaignId)
            .Distinct()
            .ToListAsync();
    }

    private static int ResolveAssetDuration(Database.Models.Asset asset, int? campaignDuration)
    {
        if (campaignDuration > 0)
            return campaignDuration.Value;

        if (asset.Type == AssetType.Video && asset.DurationSeconds > 0)
            return asset.DurationSeconds.Value;

        return DefaultDurationSeconds;
    }
}
