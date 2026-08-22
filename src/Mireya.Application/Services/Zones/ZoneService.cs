using Microsoft.EntityFrameworkCore;
using Mireya.Application.Services.Audit;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.Zones;

public interface IZoneService
{
    Task<List<ZoneSummary>> GetZonesAsync();
    Task<ZoneDetail> GetZoneAsync(Guid id);
    Task<ZoneDetail> CreateZoneAsync(CreateZoneRequest request);
    Task<ZoneDetail> UpdateZoneAsync(Guid id, UpdateZoneRequest request);
    Task DeleteZoneAsync(Guid id);
}

public class ZoneService(
    MireyaDbContext db,
    IScreenSynchronizationService syncService,
    IAuditService audit
) : IZoneService
{
    public async Task<List<ZoneSummary>> GetZonesAsync()
    {
        var zones = await db
            .Zones.Include(z => z.Displays)
            .Include(z => z.ZoneCampaigns)
            .AsSplitQuery()
            .OrderBy(z => z.Name)
            .ToListAsync();

        return zones
            .Select(z => new ZoneSummary(
                z.Id,
                z.Name,
                z.Description,
                z.Displays.Count,
                z.ZoneCampaigns.Count,
                z.CreatedAt,
                z.UpdatedAt
            ))
            .ToList();
    }

    public async Task<ZoneDetail> GetZoneAsync(Guid id)
    {
        var zone = await db
            .Zones.Include(z => z.Displays)
            .Include(z => z.ZoneCampaigns)
                .ThenInclude(zc => zc.Campaign)
            .AsSplitQuery()
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
            throw new KeyNotFoundException($"Zone with ID {id} not found");

        return MapDetail(zone);
    }

    public async Task<ZoneDetail> CreateZoneAsync(CreateZoneRequest request)
    {
        ValidateName(request.Name);
        var campaignIds = request.CampaignIds.Distinct().ToList();
        await VerifyCampaignsExistAsync(campaignIds);

        var zone = new Zone
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Zones.Add(zone);
        foreach (var campaignId in campaignIds)
            db.ZoneCampaigns.Add(new ZoneCampaign { ZoneId = zone.Id, CampaignId = campaignId });

        await db.SaveChangesAsync();

        await audit.LogAsync("Created", "Zone", zone.Id.ToString(), $"Created zone '{zone.Name}'");

        return await GetZoneAsync(zone.Id);
    }

    public async Task<ZoneDetail> UpdateZoneAsync(Guid id, UpdateZoneRequest request)
    {
        ValidateName(request.Name);
        var campaignIds = request.CampaignIds.Distinct().ToList();
        await VerifyCampaignsExistAsync(campaignIds);

        var zone = await db
            .Zones.Include(z => z.Displays)
            .Include(z => z.ZoneCampaigns)
            .AsSplitQuery()
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
            throw new KeyNotFoundException($"Zone with ID {id} not found");

        zone.Name = request.Name.Trim();
        zone.Description = request.Description;
        zone.UpdatedAt = DateTime.UtcNow;

        // Replace the zone's campaign set with the requested one.
        db.ZoneCampaigns.RemoveRange(zone.ZoneCampaigns);
        foreach (var campaignId in campaignIds)
            db.ZoneCampaigns.Add(new ZoneCampaign { ZoneId = zone.Id, CampaignId = campaignId });

        var memberIds = zone.Displays.Select(d => d.Id).ToList();

        await db.SaveChangesAsync();

        await audit.LogAsync("Updated", "Zone", zone.Id.ToString(), $"Updated zone '{zone.Name}'");

        // Campaign membership changed, so every member screen must be re-synced.
        await syncService.SyncScreensAsync(memberIds);

        return await GetZoneAsync(zone.Id);
    }

    public async Task DeleteZoneAsync(Guid id)
    {
        var zone = await db.Zones.Include(z => z.Displays).FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
            throw new KeyNotFoundException($"Zone with ID {id} not found");

        // Members are detached (ZoneId set to null via cascade) and must be re-synced afterwards.
        var memberIds = zone.Displays.Select(d => d.Id).ToList();

        db.Zones.Remove(zone);
        await db.SaveChangesAsync();

        await audit.LogAsync("Deleted", "Zone", id.ToString(), $"Deleted zone '{zone.Name}'");

        await syncService.SyncScreensAsync(memberIds);
    }

    private static ZoneDetail MapDetail(Zone zone) =>
        new(
            zone.Id,
            zone.Name,
            zone.Description,
            zone.CreatedAt,
            zone.UpdatedAt,
            zone.Displays.OrderBy(d => d.Name)
                .Select(d => new ZoneScreenInfo(d.Id, d.Name, d.Location))
                .ToList(),
            zone.ZoneCampaigns.Select(zc => new ZoneCampaignInfo(zc.CampaignId, zc.Campaign.Name))
                .OrderBy(c => c.Name)
                .ToList()
        );

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Zone name is required");
    }

    private async Task VerifyCampaignsExistAsync(List<Guid> campaignIds)
    {
        if (campaignIds.Count == 0)
            return;

        var existing = await db.Campaigns.CountAsync(c => campaignIds.Contains(c.Id));
        if (existing != campaignIds.Count)
            throw new ArgumentException("One or more campaigns do not exist");
    }
}
