using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SosuBot.Database;
using SosuBot.Database.Models;

namespace SosuBot.Render.Controllers
{
    [ApiController]
    [Authorize(Roles = "sosubot-renderer")]
    [Route("/render")]
    public class RenderController(DatabaseContext rendererContext, ILogger<RenderController> logger, IDistributedLockProvider synchronizationProvider) : ControllerBase
    {
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat()
        {
            Console.WriteLine(string.Join("\n", User.Claims.Select(m => m.ToString())) + "\n");

            var clientId = int.Parse(User.Claims.First(m => m.Type == "client-id").Value);
            if (rendererContext.Renderers.FirstOrDefault(m => m.RendererId == clientId) is not { } renderer)
            {
                logger.LogWarning($"Heartbeat error, clientId: {clientId}");
                return StatusCode(500);
            }

            renderer.LastSeen = DateTime.UtcNow;
            renderer.IsOnline = true;

            await rendererContext.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("report-rendering-progress")]
        public async Task<IActionResult> ReportRenderingProgress([FromQuery(Name = "job-id")] int jobId, [FromQuery(Name = "job-id")] double progress)
        {
            if(progress < 0 || progress > 1)
            {
                return BadRequest(new
                {
                    message = "0 <= progress <= 1"
                });
            }

            var clientId = int.Parse(User.Claims.First(m => m.Type == "client-id").Value);
            if (rendererContext.Renderers.FirstOrDefault(m => m.RendererId == clientId) is not { IsOnline: true } renderer)
            {
                return BadRequest(new
                {
                    message = "You should send a heartbeat"
                });
            }

            var renderJob = await rendererContext.RenderJobs.FirstOrDefaultAsync(r => 
                r.JobId == jobId 
                && r.RenderingBy == renderer.RendererId
                && r.IsComplete == false);
            if (renderJob == null)
            {
                return NotFound();
            }
            logger.LogInformation($"JobId: {renderJob.JobId}, old progress: {renderJob.ProgressPercent}, new progress: {progress}");
            renderJob.ProgressPercent = progress;

            await rendererContext.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("finish-rendering")]
        public async Task<IActionResult> FinishRendering([FromQuery(Name = "job-id")] int jobId)
        {
            var clientId = int.Parse(User.Claims.First(m => m.Type == "client-id").Value);
            if (rendererContext.Renderers.FirstOrDefault(m => m.RendererId == clientId) is not { IsOnline: true } renderer)
            {
                return BadRequest(new
                {
                    message = "You should send a heartbeat"
                });
            }

            var renderJob = await rendererContext.RenderJobs.FirstOrDefaultAsync(r =>
                r.JobId == jobId
                && r.RenderingBy == renderer.RendererId
                && r.IsComplete == false);
            if (renderJob == null)
            {
                return NotFound();
            }
            logger.LogInformation($"JobId: {renderJob.JobId}. Completed");
            renderJob.IsComplete = true;
            renderer.CompletedJobs.Add(renderJob);

            await rendererContext.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("get-next-render-job")]
        public async Task<IActionResult> GetNextRenderJob()
        {
            var clientId = int.Parse(User.Claims.First(m => m.Type == "client-id").Value);
            if (rendererContext.Renderers.FirstOrDefault(m => m.RendererId == clientId) is not { IsOnline: true } renderer)
            {
                return BadRequest(new
                {
                    message = "You should send a heartbeat"
                });
            }

            using (synchronizationProvider.AcquireLock("render-job-lock"))
            {
                var freeJob = await rendererContext.RenderJobs.FirstOrDefaultAsync(m => m.RenderingBy == -1);
                if (freeJob == null)
                {
                    return NotFound();
                }

                freeJob.RenderingBy = renderer.RendererId;
                return Ok(freeJob);
            }
        }

        [HttpPost("download-replay")]
        public async Task<IActionResult> DownloadReplay([FromQuery(Name = "job-id")] int jobId)
        {
            var clientId = int.Parse(User.Claims.First(m => m.Type == "client-id").Value);
            if (rendererContext.Renderers.FirstOrDefault(m => m.RendererId == clientId) is not { IsOnline: true } renderer)
            {
                return BadRequest(new
                {
                    message = "You should send a heartbeat"
                });
            }

            var renderJob = await rendererContext.RenderJobs.FirstOrDefaultAsync(r => r.JobId == jobId);
            if (renderJob == null)
            {
                return NotFound();
            }

            return PhysicalFile(renderJob.ReplayPath, "application/octet-stream", $"replay_{jobId}.osr");
        }

        [HttpPost("queue-replay")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(67108864)] // 64 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 67108864)] // 64 MB
        public async Task<IActionResult> QueueReplay([FromForm] IFormFile file, [FromHeader(Name = "Requested-By")] string requestedBy)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No replay file uploaded.");

            if (!Path.GetExtension(file.FileName).Equals(".osr", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Invalid replay file type.");

            if (file.Length > 10 * 1024 * 1024) // 10 MB safety limit
                return BadRequest("Replay file too large.");

            var datetimeUtcNow = DateTime.UtcNow;
            var replayFileName = $"{datetimeUtcNow.ToFileTimeUtc()}.osr";
            var replayDirectoryPath = Path.GetDirectoryName(Path.Combine(AppContext.BaseDirectory, "replays"))!;
            var storagePath = Path.Combine(replayDirectoryPath, replayFileName);
            Directory.CreateDirectory(replayDirectoryPath);
            using var stream = new FileStream(storagePath, FileMode.CreateNew);
            await file.CopyToAsync(stream);

            RenderJob renderJob = new RenderJob
            {
                ReplayPath = storagePath,
                RequestedAt = datetimeUtcNow,
                RequestedBy = requestedBy
            };
            await rendererContext.RenderJobs.AddAsync(renderJob);
            await rendererContext.SaveChangesAsync();

            return Accepted(new
            {
                jobId = renderJob.JobId,
                status = "queued"
            });
        }
    }
}
