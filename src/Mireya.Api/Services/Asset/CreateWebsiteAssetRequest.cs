using System.ComponentModel.DataAnnotations;

namespace Mireya.Api.Services.Asset;

public class CreateWebsiteAssetRequest
{
    [Required]
    [Url]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}
