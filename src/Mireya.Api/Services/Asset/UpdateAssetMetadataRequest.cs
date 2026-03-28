namespace Mireya.Api.Services.Asset;

public class UpdateAssetMetadataRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? DurationSeconds { get; set; }
    public bool? IsMuted { get; set; }
}
