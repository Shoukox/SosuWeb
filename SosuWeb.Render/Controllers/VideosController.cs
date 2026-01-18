using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SosuWeb.Database;
using SosuWeb.Database.Migrations;
using System.Text;

namespace SosuWeb.Render.Controllers
{
    [ApiController]
    [Route("/videos")]
    public sealed class VideosController : ControllerBase
    {
        private readonly DatabaseContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<VideosController> _logger;
        public static string VideosDir = Path.Combine(AppContext.BaseDirectory, "videos");

        public VideosController(
            DatabaseContext db,
            IWebHostEnvironment env,
            ILogger<VideosController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        [HttpGet("{fileName}")]
        [HttpHead("{fileName}")]
        public async Task<IActionResult> GetVideo(string fileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(VideosDir, fileName));

            if (!fullPath.StartsWith(VideosDir))
                return Forbid();

            Directory.CreateDirectory(VideosDir);
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            if (Path.GetExtension(fileName) != ".mp4")
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

            var renderJob = await _db.RenderJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.JobId == jobId);

            if (renderJob is null)
                return Forbid();

            _logger.LogInformation(
                "Video access granted: {File} (JobId={JobId})",
                fileName,
                jobId
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
                "video/mp4",
                enableRangeProcessing: true
            );
        }
    }
}
