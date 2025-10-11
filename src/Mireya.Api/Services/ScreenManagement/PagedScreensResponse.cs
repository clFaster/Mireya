namespace Mireya.Api.Services.ScreenManagement;

/// <summary>
/// Paged response for screens list
/// </summary>
public class PagedScreensResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ScreenDetailsResponse> Items { get; set; } = [];
}