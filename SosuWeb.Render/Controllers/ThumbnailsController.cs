using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SosuWeb.Database;
using SosuWeb.Render.Services;
using System.Text;

namespace SosuWeb.Render.Controllers
{
    [ApiController]
    [Route("/thumbnails")]
    public sealed class ThumbnailsController(DatabaseContext databaseContext, ILogger<VideosController> logger, ThumbnailService thumbnailService) : ControllerBase
    {
        public static string ThumbnailsDir = Path.Combine(AppContext.BaseDirectory, "thumbnails");

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10485760)] // 10 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 10485760)] // 10 MB
        public async Task<IActionResult> Upload(
            [FromForm] IFormFile file,
            [FromQuery(Name = "job-id")] int jobId)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("There is no thumbnail.");
            }

            if (!Path.GetExtension(file.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid thumbnail file type.");
            }

            var clientId = int.Parse(User.Claims.First(m => m.Type == "client-id").Value);
            if (databaseContext.Renderers.FirstOrDefault(m => m.RendererId == clientId) is not { IsOnline: true } renderer)
            {
                return BadRequest(new
                {
                    message = "You should send a heartbeat"
                });
            }

            var renderJob = await databaseContext.RenderJobs.FirstOrDefaultAsync(r => r.JobId == jobId);
            if (renderJob == null)
            {
                return NotFound();
            }

            Directory.CreateDirectory(ThumbnailsDir);
            var thumbnailFileName = thumbnailService.GetThumbnailFileName(renderJob.JobId, renderJob.RequestedAt);
            string thumbnailFullPath = Path.Combine(ThumbnailsDir, thumbnailFileName);
            await using (var fs = new FileStream(thumbnailFullPath, FileMode.Create, FileAccess.Write))
            {
                await file.CopyToAsync(fs);
            }

            renderJob.VideoThumbnailLocalPath = thumbnailFullPath;
            renderJob.VideoThumbnailUri = $"{Request.Scheme}://{Request.Host.ToString().Replace("localhost", "127.0.0.1")}{Request.PathBase}/thumbnails/{thumbnailFileName}";
            await databaseContext.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("{fileName}")]
        [HttpHead("{fileName}")]
        public async Task<IActionResult> GetThumbnail(string fileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(ThumbnailsDir, fileName));

            if (!fullPath.StartsWith(ThumbnailsDir))
                return Forbid();

            Directory.CreateDirectory(ThumbnailsDir);
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            if (Path.GetExtension(fileName) != ".png")
                return NotFound();

            var filenameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            Span<byte> buffer = stackalloc byte[512];
            byte[] bytes = Encoding.ASCII.GetBytes(filenameWithoutExt);
            if (!System.Buffers.Text.Base64Url.IsValid(bytes) || !System.Buffers.Text.Base64Url.TryDecodeFromUtf8(bytes, buffer, out var written))
                return Forbid();

            string decoded = Encoding.UTF8.GetString(buffer[..written]);

            var parts = decoded.Split('_', 2);
            if (!int.TryParse(parts[0], out var jobId))
                return Forbid();

            var renderJob = await databaseContext.RenderJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.JobId == jobId);

            if (renderJob is null)
                return Forbid();

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
                "image/png",
                enableRangeProcessing: true
            );
        }
    }
}
