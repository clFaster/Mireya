using Microsoft.EntityFrameworkCore;
using Mireya.Application.Constants;
using Mireya.Application.Services.Audit;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.Campaign;

public interface ICampaignService
{
    Task<List<CampaignSummary>> GetCampaignsAsync(Guid? screenId = null);
    Task<CampaignDetail> GetCampaignAsync(Guid id);
    Task<CampaignDetail> CreateCampaignAsync(CreateCampaignRequest request);
    Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request);
    Task DeleteCampaignAsync(Guid id);
    Task<List<Guid>> GetCampaignsUsingAssetAsync(Guid assetId);
}

public class CampaignService(
    MireyaDbContext db,
    IScreenSynchronizationService syncService,
    IAuditService audit
) : ICampaignService
{
    public async Task<List<CampaignSummary>> GetCampaignsAsync(Guid? screenId = null)
    {
        var query = db
            .Campaigns.Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .AsSplitQuery()
            .AsQueryable();

        if (screenId.HasValue)
            query = query.Where(c =>
                c.CampaignAssignments.Any(ca => ca.ScreenId == screenId.Value)
            );

        var campaigns = await query.OrderByDescending(c => c.UpdatedAt).ToListAsync();
        var utcNow = DateTime.UtcNow;
        return campaigns.Select(c => MapSummary(c, utcNow)).ToList();
    }

    public async Task<CampaignDetail> GetCampaignAsync(Guid id)
    {
        var campaign = await db
            .Campaigns.Include(c => c.CampaignAssets)
                .ThenInclude(ca => ca.Asset)
            .Include(c => c.CampaignAssignments)
                .ThenInclude(ca => ca.Screen)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        return MapDetail(campaign, DateTime.UtcNow);
    }

    public async Task<CampaignDetail> CreateCampaignAsync(CreateCampaignRequest request)
    {
        ValidateCampaignRequest(request.Name, request.Assets);
        await VerifyAssetsExistAsync(
            request.Assets.Select(a => a.AssetId).Distinct().ToList(),
            request.Assets.Count
        );

        var campaign = new Database.Models.Campaign
        {
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Campaigns.Add(campaign);
        AddCampaignAssets(campaign.Id, request.Assets);
        await db.SaveChangesAsync();

        await audit.LogAsync(
            "Created",
            "Campaign",
            campaign.Id.ToString(),
            $"Created campaign '{campaign.Name}'"
        );

        return await GetCampaignAsync(campaign.Id);
    }

    public async Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request)
    {
        ValidateCampaignRequest(request.Name, request.Assets);

        var campaign = await db
            .Campaigns.Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        await VerifyAssetsExistAsync(
            request.Assets.Select(a => a.AssetId).Distinct().ToList(),
            request.Assets.Count
        );

        var affectedScreenIds = campaign.CampaignAssignments.Select(a => a.ScreenId).ToList();

        campaign.Name = request.Name;
        campaign.Description = request.Description;
        campaign.UpdatedAt = DateTime.UtcNow;
        db.CampaignAssets.RemoveRange(campaign.CampaignAssets);
        AddCampaignAssets(campaign.Id, request.Assets);
        await db.SaveChangesAsync();

        await audit.LogAsync(
            "Updated",
            "Campaign",
            campaign.Id.ToString(),
            $"Updated campaign '{campaign.Name}'"
        );

        await syncService.SyncScreensAsync(affectedScreenIds);

        return await GetCampaignAsync(campaign.Id);
    }

    public async Task DeleteCampaignAsync(Guid id)
    {
        var campaign = await db
            .Campaigns.Include(c => c.CampaignAssignments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        var affectedScreenIds = campaign.CampaignAssignments.Select(a => a.ScreenId).ToList();

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();
        await audit.LogAsync(
            "Deleted",
            "Campaign",
            id.ToString(),
            $"Deleted campaign '{campaign.Name}'"
        );

        await syncService.SyncScreensAsync(affectedScreenIds);
    }

    public Task<List<Guid>> GetCampaignsUsingAssetAsync(Guid assetId) =>
        db
            .CampaignAssets.Where(ca => ca.AssetId == assetId)
            .Select(ca => ca.CampaignId)
            .Distinct()
            .ToListAsync();

    private static CampaignSummary MapSummary(Database.Models.Campaign campaign, DateTime utcNow) =>
        new(
            campaign.Id,
            campaign.Name,
            campaign.Description,
            campaign.CampaignAssets.Count,
            campaign.CampaignAssignments.Count,
            campaign.CreatedAt,
            campaign.UpdatedAt,
            campaign.CampaignAssignments.Count(a => a.IsActiveAt(utcNow))
        );

    private static CampaignDetail MapDetail(Database.Models.Campaign campaign, DateTime utcNow) =>
        new(
            campaign.Id,
            campaign.Name,
            campaign.Description,
            campaign
                .CampaignAssets.OrderBy(ca => ca.Position)
                .Select(ca => new CampaignAssetDetail(
                    ca.Id,
                    ca.AssetId,
                    ca.Asset.Name,
                    ca.Asset.Type,
                    ca.Asset.Source,
                    ca.Position,
                    ca.DurationSeconds,
                    AssetDurationResolver.Resolve(ca.Asset, ca.DurationSeconds),
                    ca.Asset.IsMuted,
                    ca.Asset.ImageFit
                ))
                .ToList(),
            campaign
                .CampaignAssignments.Select(a => CampaignAssignmentPolicy.ToDetail(a, utcNow))
                .ToList(),
            campaign.CreatedAt,
            campaign.UpdatedAt
        );

    private static void ValidateCampaignRequest(string name, List<CampaignAssetDto> assets)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Campaign name is required");
        if (assets.Any(a => a.Position <= 0))
            throw new ArgumentException("Asset positions must be positive integers");
        if (assets.Any(a => a.DurationSeconds is <= 0))
            throw new ArgumentException("Duration must be positive if provided");
    }

    private async Task VerifyAssetsExistAsync(List<Guid> assetIds, int requestedCount)
    {
        if (assetIds.Count != requestedCount)
            throw new ArgumentException("Duplicate assets are not allowed in a campaign");

        var existingIds = await db
            .Assets.Where(a => assetIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync();
        var missingIds = assetIds.Except(existingIds).ToList();
        if (missingIds.Count > 0)
            throw new ArgumentException(
                $"One or more assets do not exist. Missing asset IDs: {string.Join(", ", missingIds)}"
            );
    }

    private void AddCampaignAssets(Guid campaignId, List<CampaignAssetDto> assets)
    {
        foreach (var asset in assets)
            db.CampaignAssets.Add(
                new CampaignAsset
                {
                    CampaignId = campaignId,
                    AssetId = asset.AssetId,
                    Position = asset.Position,
                    DurationSeconds = asset.DurationSeconds,
                }
            );
    }
}
