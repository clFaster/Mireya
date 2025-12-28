namespace Mireya.Api.Services.Asset;

public class PagedAssets
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<Database.Models.Asset> Items { get; set; } = [];
}
