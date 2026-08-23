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
                .ThenInclude(ca => ca.Screen)
            .AsSplitQuery()
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
                ca.Asset.IsMuted,
                ca.Asset.ImageFit
            ))
            .ToList();

        var screens = campaign
            .CampaignAssignments.Select(ca => new ScreenInfo(
                ca.Screen.Id,
                ca.Screen.Name,
                ca.Screen.Location
            ))
            .ToList();

        return new CampaignDetail(
            campaign.Id,
            campaign.Name,
            campaign.Description,
            assets,
            screens,
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
        ValidateRecurrence(
            request.DailyStartTime,
            request.DailyEndTime,
            request.RecurrenceTimeZoneId
        );

        await VerifyAssetsExistAsync(
            request.Assets.Select(a => a.AssetId).Distinct().ToList(),
            request.Assets.Count
        );

        if (request.ScreenIds.Any())
            await VerifyScreensExistAsync(request.ScreenIds);

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
            RecurrenceTimeZoneId = string.IsNullOrWhiteSpace(request.RecurrenceTimeZoneId)
                ? null
                : request.RecurrenceTimeZoneId,
        };

        db.Campaigns.Add(campaign);
        AddCampaignAssets(campaign.Id, request.Assets);
        AddCampaignAssignments(campaign.Id, request.ScreenIds);

        await SaveWithSingleDefaultAsync(campaign, request.IsDefault);

        var campaignDetail = await GetCampaignAsync(campaign.Id);

        await audit.LogAsync(
            "Created",
            "Campaign",
            campaign.Id.ToString(),
            $"Created campaign '{campaign.Name}'"
        );

        // A new default campaign affects every screen that currently has nothing active.
        if (request.IsDefault)
            await SyncAllScreensAsync();
        else
            await syncService.SyncScreensAsync(request.ScreenIds);

        return campaignDetail;
    }

    public async Task<CampaignDetail> UpdateCampaignAsync(Guid id, UpdateCampaignRequest request)
    {
        ValidateCampaignRequest(request.Name, request.Assets);
        ValidateSchedule(request.StartDateUtc, request.EndDateUtc);
        ValidateRecurrence(
            request.DailyStartTime,
            request.DailyEndTime,
            request.RecurrenceTimeZoneId
        );

        var campaign = await db
            .Campaigns.Include(c => c.CampaignAssets)
            .Include(c => c.CampaignAssignments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        var oldScreenIds = campaign.CampaignAssignments.Select(ca => ca.ScreenId).ToList();
        var defaultChanged = campaign.IsDefault != request.IsDefault;

        await VerifyAssetsExistAsync(
            request.Assets.Select(a => a.AssetId).Distinct().ToList(),
            request.Assets.Count
        );

        if (request.ScreenIds is { Count: > 0 })
            await VerifyScreensExistAsync(request.ScreenIds);

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
        campaign.RecurrenceTimeZoneId = string.IsNullOrWhiteSpace(request.RecurrenceTimeZoneId)
            ? null
            : request.RecurrenceTimeZoneId;

        db.CampaignAssets.RemoveRange(campaign.CampaignAssets);
        AddCampaignAssets(campaign.Id, request.Assets);

        // Only modify screen assignments when explicitly provided.
        // null => leave assignments untouched (e.g. campaign content edits).
        // non-null list (incl. empty) => set assignments to exactly this set.
        var affectedScreenIds = oldScreenIds;
        if (request.ScreenIds is not null)
        {
            db.CampaignAssignments.RemoveRange(campaign.CampaignAssignments);
            AddCampaignAssignments(campaign.Id, request.ScreenIds);
            affectedScreenIds = oldScreenIds.Union(request.ScreenIds).ToList();
        }

        await SaveWithSingleDefaultAsync(campaign, request.IsDefault);

        var campaignDetail = await GetCampaignAsync(campaign.Id);

        await audit.LogAsync(
            "Updated",
            "Campaign",
            campaign.Id.ToString(),
            $"Updated campaign '{campaign.Name}'"
        );

        // Changing the default designation (or editing the default campaign's content)
        // can affect any screen that relies on the fallback, so re-sync everything.
        if (defaultChanged || campaign.IsDefault)
            await SyncAllScreensAsync();
        else
            await syncService.SyncScreensAsync(affectedScreenIds);

        return campaignDetail;
    }

    public async Task DeleteCampaignAsync(Guid id)
    {
        var campaign = await db
            .Campaigns.Include(c => c.CampaignAssignments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            throw new KeyNotFoundException($"Campaign with ID {id} not found");

        // Collect affected screens before deletion (cascade will remove assignments)
        var affectedScreenIds = campaign.CampaignAssignments.Select(ca => ca.ScreenId).ToList();

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();

        await audit.LogAsync(
            "Deleted",
            "Campaign",
            id.ToString(),
            $"Deleted campaign '{campaign.Name}'"
        );

        // Notify affected screens so they stop showing deleted campaign content
        if (affectedScreenIds.Count > 0)
            await syncService.SyncScreensAsync(affectedScreenIds);
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
            throw new ArgumentException(
                "Campaign end date must not be earlier than its start date"
            );
    }

    private static void ValidateRecurrence(TimeOnly? start, TimeOnly? end, string? timeZoneId)
    {
        if (start.HasValue != end.HasValue)
            throw new ArgumentException(
                "Daily start and end time must both be set or both be empty"
            );

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

    private async Task VerifyScreensExistAsync(List<Guid> screenIds)
    {
        var existingScreens = await db.Screens.Where(d => screenIds.Contains(d.Id)).CountAsync();
        if (existingScreens != screenIds.Distinct().Count())
            throw new ArgumentException("One or more screens do not exist");
    }

    private async Task ClearOtherDefaultsAsync(Guid keepCampaignId)
    {
        var otherDefaults = await db
            .Campaigns.Where(c => c.IsDefault && c.Id != keepCampaignId)
            .ToListAsync();
        foreach (var other in otherDefaults)
            other.IsDefault = false;
    }

    private async Task SaveWithSingleDefaultAsync(
        Database.Models.Campaign campaign,
        bool shouldBeDefault
    )
    {
        if (!shouldBeDefault)
        {
            await db.SaveChangesAsync();
            return;
        }

        // Two saves inside one transaction avoid a temporary unique-index conflict
        // while switching the default from one campaign to another.
        await using var transaction = await db.Database.BeginTransactionAsync();
        campaign.IsDefault = false;
        await ClearOtherDefaultsAsync(campaign.Id);
        await db.SaveChangesAsync();

        campaign.IsDefault = true;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task SyncAllScreensAsync()
    {
        var screenIds = await db.Screens.Select(d => d.Id).ToListAsync();
        await syncService.SyncScreensAsync(screenIds);
    }

    private void AddCampaignAssets(Guid campaignId, List<CampaignAssetDto> assets)
    {
        foreach (var assetDto in assets)
            db.CampaignAssets.Add(
                new CampaignAsset
                {
                    CampaignId = campaignId,
                    AssetId = assetDto.AssetId,
                    Position = assetDto.Position,
                    DurationSeconds = assetDto.DurationSeconds,
                }
            );
    }

    private void AddCampaignAssignments(Guid campaignId, List<Guid> screenIds)
    {
        foreach (var screenId in screenIds)
            db.CampaignAssignments.Add(
                new CampaignAssignment { CampaignId = campaignId, ScreenId = screenId }
            );
    }
}
