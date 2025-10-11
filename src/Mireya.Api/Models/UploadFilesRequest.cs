using Microsoft.AspNetCore.Mvc;

namespace Mireya.Api.Models;

public class UploadFilesRequest
{
    [FromForm(Name = "files")]
    public List<IFormFile> Files { get; set; } = [];
}
