using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SosuWeb.Database;
using SosuWeb.Render.Services;

namespace SosuWeb.Render.Controllers
{
    [ApiController]
    [Route("/skins")]
    public sealed class SkinsController(DatabaseContext dbContext, ILogger<VideosController> logger, SkinService skinService) : ControllerBase
    {
        public static string SkinsDir = Path.Combine(AppContext.BaseDirectory, "skins");

        [HttpGet("{skinFileName}")]
        [HttpHead("{skinFileName}")]
        public async Task<IActionResult> GetSkin(string skinFileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(SkinsDir, skinFileName));

            if (!fullPath.StartsWith(SkinsDir))
                return Forbid();

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            if (Path.GetExtension(skinFileName) != ".osk")
                return NotFound();

            logger.LogInformation(
                $"Skin access granted. Skin: {skinFileName}"
            );

            var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true
            );

            return File(
                stream,
                "application/octet-stream",
                fileDownloadName: skinService.SkinFileNameFromHex(skinFileName),
                enableRangeProcessing: false
            );
        }

        [HttpGet("get-available-skins")]
        public async Task<IActionResult> GetAvailableSkins()
        {
            if (Directory.Exists(SkinsDir))
            {
                IEnumerable<string> availableSkins = Directory.GetFiles(SkinsDir).Select(m => skinService.SkinFileNameFromHex(Path.GetFileName(m)));
                return Ok(availableSkins);
            }
            else
            {
                return NotFound(); 
            }
        }

        [HttpPost("upload-skin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(167772160)] // 160 MB (+10)
        [RequestFormLimits(MultipartBodyLengthLimit = 157286400)] // 150 MB
        public async Task<IActionResult> UploadSkin([FromForm] IFormFile skinFile)
        {
            if (skinFile == null || skinFile.Length == 0)
                return BadRequest("There is no skin file.");

            if (!Path.GetExtension(skinFile.FileName).Equals(".osk", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Invalid skin file type.");

            Directory.CreateDirectory(SkinsDir);
            var skinFileNameAsHex = skinService.SkinFileNameToHex(skinFile.FileName);
            var storagePath = Path.Combine(SkinsDir, skinFileNameAsHex);

            using var stream = new FileStream(storagePath, FileMode.Create);
            await skinFile.CopyToAsync(stream);

            logger.LogInformation($"Skin {skinFile.FileName} was uploaded");
            return Ok(new
            {
                location = $"skins/{skinFileNameAsHex}"
            });
        }
    }
}
