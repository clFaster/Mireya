using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;

namespace Mireya.Web.Services;

/// <summary>
/// Adapts Blazor's IBrowserFile to IFormFile so it can be passed to AssetService.
/// </summary>
public class BrowserIFormFile(IBrowserFile file) : IFormFile
{
    private const long MaxFileSize = 100 * 1024 * 1024; // 100 MB

    public string ContentType => file.ContentType;
    public string ContentDisposition => $"form-data; name=\"files\"; filename=\"{file.Name}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long Length => file.Size;
    public string Name => "files";
    public string FileName => file.Name;

    public void CopyTo(Stream target)
    {
        using var stream = file.OpenReadStream(MaxFileSize);
        stream.CopyTo(target);
    }

    public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream(MaxFileSize);
        await stream.CopyToAsync(target, cancellationToken);
    }

    public Stream OpenReadStream() => file.OpenReadStream(MaxFileSize);
}
