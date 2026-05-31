namespace Mireya.Application.Services.Zones;

public record ZoneSummary(
    Guid Id,
    string Name,
    string? Description,
    int ScreenCount,
    int CampaignCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ZoneScreenInfo(Guid Id, string Name, string? Location);

public record ZoneCampaignInfo(Guid Id, string Name);

public record ZoneDetail(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ZoneScreenInfo> Screens,
    List<ZoneCampaignInfo> Campaigns
);

public record CreateZoneRequest(string Name, string? Description, List<Guid> CampaignIds);

public record UpdateZoneRequest(string Name, string? Description, List<Guid> CampaignIds);
