using Microsoft.EntityFrameworkCore;
using Mireya.Application.Constants;
using Mireya.Application.Services.Audit;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.Campaign;

public interface ICampaignService
{
    Task<List<CampaignSummary>> GetCampaignsAsync(Guid? displayId = null);
    Task<CampaignDetail> GetCampaignAsync(Guid id);
    Task<CampaignDetail> CreateCampaignAsync(CreateCampaignRequest request);
    Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request);
    Task DeleteCampaignAsync(Guid id);
    Task<List<Guid>> GetCampaignsUsingAssetAsync(Guid assetId);
}

public class CampaignService(MireyaDbContext db, IScreenSynchronizationService syncService, IAuditService audit)
    : ICampaignService
{

    public async Task<List<CampaignSummary>> GetCampaignsAsync(Guid? displayId = null)
    {
        var query = db
            .Campaigns.Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .AsQueryable();

        if (displayId.HasValue)
            query = query.Where(c =>
                c.CampaignAssignments.Any(ca => ca.DisplayId == displayId.Value)
            );

        var campaigns = await query.OrderByDescending(c => c.UpdatedAt).ToListAsync();

        return campaigns
            .Select(c => new CampaignSummary(
                c.Id,
                c.Name,
                c.Description,
                c.CampaignAssets.Count,
                c.CampaignAssignments.Count,
                c.CreatedAt,
                c.UpdatedAt,
                c.IsEnabled,
                c.StartDateUtc,
                c.EndDateUtc,
                c.IsActiveAt(DateTime.UtcNow),
                c.Priority,
                c.IsDefault
            ))
            .ToList();
    }

    public async Task<CampaignDetail> GetCampaignAsync(Guid id)
    {
        var campaign = await db
            .Campaigns.Include(c => c.CampaignAssets)
                .ThenInclude(ca => ca.Asset)
            .Include(c => c.CampaignAssignments)
                .ThenInclude(ca => ca.Display)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        var assets = campaign
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
                ca.Asset.IsMuted
            ))
            .ToList();

        var displays = campaign
            .CampaignAssignments.Select(ca => new DisplayInfo(
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
            campaign.UpdatedAt,
            campaign.IsEnabled,
            campaign.StartDateUtc,
            campaign.EndDateUtc,
            campaign.Priority,
            campaign.IsDefault,
            campaign.RecurrenceDaysMask,
            campaign.DailyStartTime,
            campaign.DailyEndTime,
            campaign.RecurrenceTimeZoneId
        );
    }

    public async Task<CampaignDetail> CreateCampaignAsync(CreateCampaignRequest request)
    {
        ValidateCampaignRequest(request.Name, request.Assets);
        ValidateSchedule(request.StartDateUtc, request.EndDateUtc);
        ValidateRecurrence(request.DailyStartTime, request.DailyEndTime, request.RecurrenceTimeZoneId);

        await VerifyAssetsExistAsync(request.Assets.Select(a => a.AssetId).Distinct().ToList(), request.Assets.Count);

        if (request.DisplayIds.Any())
            await VerifyDisplaysExistAsync(request.DisplayIds);

        var campaign = new Database.Models.Campaign
        {
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsEnabled = request.IsEnabled,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            Priority = request.Priority,
            IsDefault = request.IsDefault,
            RecurrenceDaysMask = NormalizeDaysMask(request.RecurrenceDaysMask),
            DailyStartTime = request.DailyStartTime,
            DailyEndTime = request.DailyEndTime,
            RecurrenceTimeZoneId = string.IsNullOrWhiteSpace(request.RecurrenceTimeZoneId) ? null : request.RecurrenceTimeZoneId,
        };

        db.Campaigns.Add(campaign);
        AddCampaignAssets(campaign.Id, request.Assets);
        AddCampaignAssignments(campaign.Id, request.DisplayIds);

        if (request.IsDefault)
            await ClearOtherDefaultsAsync(campaign.Id);

        await db.SaveChangesAsync();

        var campaignDetail = await GetCampaignAsync(campaign.Id);

        await audit.LogAsync("Created", "Campaign", campaign.Id.ToString(), $"Created campaign '{campaign.Name}'");

        // A new default campaign affects every screen that currently has nothing active.
        if (request.IsDefault)
            await SyncAllDisplaysAsync();
        else
            await syncService.SyncScreensAsync(request.DisplayIds);

        return campaignDetail;
    }

    public async Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request)
    {
        ValidateCampaignRequest(request.Name, request.Assets);
        ValidateSchedule(request.StartDateUtc, request.EndDateUtc);
        ValidateRecurrence(request.DailyStartTime, request.DailyEndTime, request.RecurrenceTimeZoneId);

        var campaign = await db
            .Campaigns.Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        var oldDisplayIds = campaign.CampaignAssignments.Select(ca => ca.DisplayId).ToList();
        var defaultChanged = campaign.IsDefault != request.IsDefault;

        await VerifyAssetsExistAsync(request.Assets.Select(a => a.AssetId).Distinct().ToList(), request.Assets.Count);

        if (request.DisplayIds is { Count: > 0 })
            await VerifyDisplaysExistAsync(request.DisplayIds);

        campaign.Name = request.Name;
        campaign.Description = request.Description;
        campaign.UpdatedAt = DateTime.UtcNow;
        campaign.IsEnabled = request.IsEnabled;
        campaign.StartDateUtc = request.StartDateUtc;
        campaign.EndDateUtc = request.EndDateUtc;
        campaign.Priority = request.Priority;
        campaign.IsDefault = request.IsDefault;
        campaign.RecurrenceDaysMask = NormalizeDaysMask(request.RecurrenceDaysMask);
        campaign.DailyStartTime = request.DailyStartTime;
        campaign.DailyEndTime = request.DailyEndTime;
        campaign.RecurrenceTimeZoneId = string.IsNullOrWhiteSpace(request.RecurrenceTimeZoneId) ? null : request.RecurrenceTimeZoneId;

        if (request.IsDefault)
            await ClearOtherDefaultsAsync(campaign.Id);

        db.CampaignAssets.RemoveRange(campaign.CampaignAssets);
        AddCampaignAssets(campaign.Id, request.Assets);

        // Only modify display assignments when explicitly provided.
        // null => leave assignments untouched (e.g. campaign content edits).
        // non-null list (incl. empty) => set assignments to exactly this set.
        var affectedDisplayIds = oldDisplayIds;
        if (request.DisplayIds is not null)
        {
            db.CampaignAssignments.RemoveRange(campaign.CampaignAssignments);
            AddCampaignAssignments(campaign.Id, request.DisplayIds);
            affectedDisplayIds = oldDisplayIds.Union(request.DisplayIds).ToList();
        }

        await db.SaveChangesAsync();

        var campaignDetail = await GetCampaignAsync(campaign.Id);

        await audit.LogAsync("Updated", "Campaign", campaign.Id.ToString(), $"Updated campaign '{campaign.Name}'");

        // Changing the default designation (or editing the default campaign's content)
        // can affect any screen that relies on the fallback, so re-sync everything.
        if (defaultChanged || campaign.IsDefault)
            await SyncAllDisplaysAsync();
        else
            await syncService.SyncScreensAsync(affectedDisplayIds);

        return campaignDetail;
    }

    public async Task DeleteCampaignAsync(Guid id)
    {
        var campaign = await db.Campaigns
            .Include(c => c.CampaignAssignments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        // Collect affected displays before deletion (cascade will remove assignments)
        var affectedDisplayIds = campaign.CampaignAssignments.Select(ca => ca.DisplayId).ToList();

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();

        await audit.LogAsync("Deleted", "Campaign", id.ToString(), $"Deleted campaign '{campaign.Name}'");

        // Notify affected screens so they stop showing deleted campaign content
        if (affectedDisplayIds.Count > 0)
            await syncService.SyncScreensAsync(affectedDisplayIds);
    }

    public async Task<List<Guid>> GetCampaignsUsingAssetAsync(Guid assetId)
    {
        return await db
            .CampaignAssets.Where(ca => ca.AssetId == assetId)
            .Select(ca => ca.CampaignId)
            .Distinct()
            .ToListAsync();
    }

    private static void ValidateCampaignRequest(string name, List<CampaignAssetDto> assets)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Campaign name is required");

        if (assets.Any(a => a.Position <= 0))
            throw new ArgumentException("Asset positions must be positive integers");

        if (assets.Any(a => a.DurationSeconds.HasValue && a.DurationSeconds.Value <= 0))
            throw new ArgumentException("Duration must be positive if provided");
    }

    private static void ValidateSchedule(DateTime? startDateUtc, DateTime? endDateUtc)
    {
        if (startDateUtc.HasValue && endDateUtc.HasValue && endDateUtc.Value < startDateUtc.Value)
            throw new ArgumentException("Campaign end date must not be earlier than its start date");
    }

    private static void ValidateRecurrence(TimeOnly? start, TimeOnly? end, string? timeZoneId)
    {
        if (start.HasValue != end.HasValue)
            throw new ArgumentException("Daily start and end time must both be set or both be empty");

        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new ArgumentException($"Unknown time zone '{timeZoneId}'");
            }
        }
    }

    /// <summary>
    ///     Normalises a weekday bitmask: 0 (no days) or 127 (all days) both mean "every day" (null).
    /// </summary>
    private static int? NormalizeDaysMask(int? mask) =>
        mask is null or 0 or 0b111_1111 ? null : mask & 0b111_1111;

    private async Task VerifyAssetsExistAsync(List<Guid> assetIds, int totalRequestedCount)
    {
        if (assetIds.Count != totalRequestedCount)
            throw new ArgumentException("Duplicate assets are not allowed in a campaign");

        var existingAssets = await db.Assets.Where(a => assetIds.Contains(a.Id)).ToListAsync();
        if (existingAssets.Count != assetIds.Count)
        {
            var missingIds = assetIds.Except(existingAssets.Select(a => a.Id)).ToList();
            throw new ArgumentException(
                $"One or more assets do not exist. Missing asset IDs: {string.Join(", ", missingIds)}"
            );
        }
    }

    private async Task VerifyDisplaysExistAsync(List<Guid> displayIds)
    {
        var existingDisplays = await db.Displays.Where(d => displayIds.Contains(d.Id)).CountAsync();
        if (existingDisplays != displayIds.Distinct().Count())
            throw new ArgumentException("One or more displays do not exist");
    }

    private async Task ClearOtherDefaultsAsync(Guid keepCampaignId)
    {
        var otherDefaults = await db.Campaigns
            .Where(c => c.IsDefault && c.Id != keepCampaignId)
            .ToListAsync();
        foreach (var other in otherDefaults)
            other.IsDefault = false;
    }

    private async Task SyncAllDisplaysAsync()
    {
        var displayIds = await db.Displays.Select(d => d.Id).ToListAsync();
        await syncService.SyncScreensAsync(displayIds);
    }

    private void AddCampaignAssets(Guid campaignId, List<CampaignAssetDto> assets)
    {
        foreach (var assetDto in assets)
            db.CampaignAssets.Add(new CampaignAsset
            {
                CampaignId = campaignId,
                AssetId = assetDto.AssetId,
                Position = assetDto.Position,
                DurationSeconds = assetDto.DurationSeconds,
            });
    }

    private void AddCampaignAssignments(Guid campaignId, List<Guid> displayIds)
    {
        foreach (var displayId in displayIds)
            db.CampaignAssignments.Add(new CampaignAssignment
            {
                CampaignId = campaignId,
                DisplayId = displayId,
            });
    }
}
