using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SosuWeb.Database;
using SosuWeb.Database.Models;
using SosuWeb.Render.Services;
using System.Text.Json;

namespace SosuWeb.Render.Controllers
{
    [ApiController]
    [Route("/render")]
    public class RenderController(DatabaseContext rendererContext, ILogger<RenderController> logger, IDistributedLockProvider synchronizationProvider, VideoService videoService, SkinService skinService) : ControllerBase
    {
        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat()
        {
            Console.WriteLine(string.Join(";", User.Claims.Select(m => m.ToString())) + "\n");

            var clientId = int.Parse(User.Claims.First(m => m.Type == "client-id").Value);
            if (rendererContext.Renderers.FirstOrDefault(m => m.RendererId == clientId) is not { } renderer)
            {
                logger.LogWarning($"Heartbeat error, clientId: {clientId}");
                return StatusCode(500);
            }

            renderer.LastSeen = DateTime.UtcNow;
            renderer.IsOnline = true;

            await rendererContext.SaveChangesAsync();
            logger.LogInformation("Heartbeat received from renderer " + clientId);
            return Ok();
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("report-rendering-progress")]
        public async Task<IActionResult> ReportRenderingProgress([FromQuery(Name = "job-id")] int jobId, [FromQuery] double progress)
        {
            if (progress > 1)
            {
                return BadRequest(new
                {
                    message = "0 <= progress <= 1 or progress is {-2, -1}"
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
            logger.LogInformation($"JobId: {renderJob.JobId}, progress: {progress}");
            renderJob.ProgressPercent = progress;
            renderJob.RenderingLastUpdate = DateTime.UtcNow;

            await rendererContext.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "sosubot-renderer")]
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
            renderJob.RenderingLastUpdate = DateTime.UtcNow;
            renderJob.IsComplete = true;
            renderJob.IsSuccess = true;
            renderJob.VideoUri = $"{Request.Scheme}://{Request.Host.ToString().Replace("localhost", "127.0.0.1")}{Request.PathBase}/videos/{videoService.GetReplayVideoFileName(renderJob.JobId, renderJob.RequestedAt)}";
            renderer.CompletedJobs.Add(renderJob);

            await rendererContext.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "sosubot-renderer")]
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

            await using (await synchronizationProvider.AcquireLockAsync("render-job-lock"))
            {
                var freeJob = await rendererContext.RenderJobs.FirstOrDefaultAsync(m => !m.IsComplete && m.RenderingBy == -1);
                if (freeJob == null)
                {
                    return NotFound();
                }

                logger.LogInformation($"Assigning JobId: {freeJob.JobId} to RendererId: {renderer.RendererId}");
                freeJob.RenderingBy = renderer.RendererId;
                freeJob.RenderingStartedAt = DateTime.UtcNow;
                freeJob.RenderingLastUpdate = DateTime.UtcNow;

                if (clientId is 1234 or 1235)
                {
                    freeJob.RenderSettings.VideoWidth = 1920;
                    freeJob.RenderSettings.VideoHeight = 1080;
                }

                await rendererContext.SaveChangesAsync();
                return Ok(freeJob);
            }
        }

        [Authorize(Roles = "sosubot-renderer")]
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
            logger.LogInformation($"JobId: {renderJob.JobId}. Replay downloaded");
            return PhysicalFile(renderJob.ReplayPath, "application/octet-stream", $"replay_{jobId}.osr");
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("failure")]
        public async Task<IActionResult> Failure([FromQuery(Name = "job-id")] int jobId, [FromQuery(Name = "reason")] string failureReason, [FromQuery] bool rerender = true)
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
                && r.IsComplete == false
                && r.RenderingBy == renderer.RendererId);
            if (renderJob == null)
            {
                return Forbid();
            }

            renderJob.IsSuccess = false;
            renderJob.FailureReason = failureReason;
            renderJob.IsComplete = !rerender;
            renderJob.RenderingStartedAt = default;
            renderJob.RenderingLastUpdate = default;
            renderJob.ProgressPercent = 0;
            await rendererContext.SaveChangesAsync();
            logger.LogInformation($"JobId: {renderJob.JobId}. Failed: {failureReason}");
            return Ok();
        }

        [HttpPost("queue-replay")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(67108864)] // 64 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 67108864)] // 64 MB
        public async Task<IActionResult> QueueReplay(
            [FromForm(Name = "file")] IFormFile file,
            [FromForm(Name = "config")] string configAsStringJson,
            [FromHeader(Name = "Requested-By")] string requestedBy)
        {
            if (file == null || file.Length == 0)
            {
                logger.LogWarning($"No replay file uploaded");
                return BadRequest("No replay file uploaded.");
            }

            if (!Path.GetExtension(file.FileName).Equals(".osr", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning($"Invalid replay file type.");
                return BadRequest("Invalid replay file type.");
            }

            var config = JsonSerializer.Deserialize<DanserConfiguration>(configAsStringJson)!;

            if (config.SkinName != "default")
            {
                if (!Path.GetExtension(config.SkinName)!.Equals(".osk", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning($"Invalid skin file type.");
                    return BadRequest("Invalid skin file type.");
                }

                string skinsDirectoryPath = SkinsController.SkinsDir;
                string skinFileNameHex = skinService.SkinFileNameToHex(config.SkinName);
                string skinPath = Path.Combine(skinsDirectoryPath, skinFileNameHex);
                if (!System.IO.File.Exists(skinPath))
                {
                    logger.LogWarning($"You should firstly upload this skin");
                    return BadRequest("You should firstly upload this skin");
                }
            }

            var datetimeUtcNow = DateTime.UtcNow;
            var replayFileName = $"{datetimeUtcNow.ToFileTimeUtc()}.osr";
            var replayDirectoryPath = Path.Combine(AppContext.BaseDirectory, "replays")!;
            var storagePath = Path.Combine(replayDirectoryPath, replayFileName);
            Directory.CreateDirectory(replayDirectoryPath);
            using var stream = new FileStream(storagePath, FileMode.CreateNew);
            await file.CopyToAsync(stream);

            RenderJob renderJob = new RenderJob
            {
                ReplayPath = storagePath,
                RequestedAt = datetimeUtcNow,
                RequestedBy = requestedBy,
                RenderSettings = config
            };
            await rendererContext.RenderJobs.AddAsync(renderJob);
            await rendererContext.SaveChangesAsync();

            logger.LogInformation($"New render job queued. JobId: {renderJob.JobId}, RequestedBy: {requestedBy}");
            return Accepted(new
            {
                jobId = renderJob.JobId,
                status = "queued"
            });
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("upload-replay-videofile")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(99614720)] // 95 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 99614720)] // 95 MB
        public async Task<IActionResult> UploadReplayVideofile(
            [FromForm] IFormFile file,
            [FromQuery(Name = "job-id")] int jobId,
            [FromQuery(Name = "chunk-index")] int chunkIndex,
            [FromQuery(Name = "total-chunks")] int totalChunks)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("There is no replay video.");
            }

            if (!Path.GetExtension(file.FileName).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid replay video file type.");
            }

            if (chunkIndex >= totalChunks)
            {
                return BadRequest("Invalid chunk index");
            }

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

            string chunkFileNameFormat = "{0}_part{1}.mp4";
            Directory.CreateDirectory(VideosController.VideosDir);
            string chunkPath = Path.Combine(VideosController.VideosDir, string.Format(chunkFileNameFormat, jobId, chunkIndex));
            await using (var fs = new FileStream(chunkPath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }
            logger.LogInformation($"JobId: {renderJob.JobId}. Got chunk {chunkIndex + 1}/{totalChunks}");

            if (chunkIndex + 1 == totalChunks)
            {
                // Reassemble
                var replayVideoFileName = videoService.GetReplayVideoFileName(renderJob.JobId, renderJob.RequestedAt);
                string finalFile = Path.Combine(VideosController.VideosDir, replayVideoFileName);
                using var output = new FileStream(finalFile, FileMode.Create);
                for (int i = 0; i < totalChunks; i++)
                {
                    string partPath = Path.Combine(VideosController.VideosDir, string.Format(chunkFileNameFormat, jobId, i));
                    await using (var partStream = new FileStream(partPath, FileMode.Open))
                    {
                        await partStream.CopyToAsync(output);
                    }
                    System.IO.File.Delete(partPath);
                }
            }

            return Ok();
        }

        [HttpGet("get-online-renderers")]
        public async Task<IActionResult> GetOnlineRenderers()
        {
            var onlineRenderers = await rendererContext.Renderers
                .Where(r => r.IsOnline)
                .Select(r => new
                {
                    r.RendererId,
                    r.LastSeen,
                })
                .ToListAsync();
            return Ok(onlineRenderers);
        }

        [HttpPost("get-render-job-info")]
        public async Task<IActionResult> GetRenderJobInfo([FromQuery(Name = "job-id")] int jobId)
        {
            var renderJob = await rendererContext.RenderJobs.FirstOrDefaultAsync(r => r.JobId == jobId);
            if (renderJob == null)
            {
                return NotFound();
            }
            return Ok(renderJob);
        }
    }
}
