using Microsoft.AspNetCore.Mvc;

namespace Mireya.Api.Services.Asset;

public class UploadFilesRequest
{
    [FromForm(Name = "files")]
    public List<IFormFile> Files { get; set; } = [];
}
