using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SosuWeb.Render.Services;

namespace SosuWeb.Render.Controllers
{
    [ApiController]
    [Route("/render")]
    public class RenderController(RenderService renderService, ILogger<RenderController> logger) : ControllerBase
    {
        private int GetClientId() => int.Parse(User.Claims.First(m => m.Type == "client-id").Value);

        private ObjectResult RendererHeartbeatRequired()
            => BadRequest(new { message = "You should send a heartbeat" });

        private IActionResult FromMutationResult(RenderJobMutationResult result, bool forbidOnMissingAssignment = false)
            => result.Status switch
            {
                RenderJobMutationStatus.Success => Ok(),
                RenderJobMutationStatus.RendererOffline => RendererHeartbeatRequired(),
                RenderJobMutationStatus.JobNotFound => NotFound(),
                RenderJobMutationStatus.Forbidden => Forbid(),
                RenderJobMutationStatus.RendererJobMismatch when forbidOnMissingAssignment => Forbid(),
                RenderJobMutationStatus.RendererJobMismatch => Conflict(new { message = "This renderer is not assigned to the specified active job." }),
                _ => StatusCode(500)
            };

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
        {
            Console.WriteLine(string.Join(";", User.Claims.Select(m => m.ToString())) + "\n");

            var ok = await renderService.HeartbeatAsync(GetClientId(), cancellationToken);
            if (!ok)
            {
                return StatusCode(500);
            }

            return Ok();
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("report-rendering-progress")]
        public async Task<IActionResult> ReportRenderingProgress([FromQuery(Name = "job-id")] int jobId, [FromQuery] double progress, CancellationToken cancellationToken)
        {
            if (progress > 1)
            {
                return BadRequest(new { message = "0 <= progress <= 1 or progress is {-2, -1}" });
            }

            var result = await renderService.ReportRenderingProgressAsync(GetClientId(), jobId, progress, cancellationToken);
            return FromMutationResult(result);
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("set-renderjob-metadata")]
        public async Task<IActionResult> SetRenderJobMetadata([FromQuery(Name = "job-id")] int jobId, CancellationToken cancellationToken)
        {
            logger.LogInformation("JobId: {JobId}. Setting metadata", jobId);
            var result = await renderService.SetRenderJobMetadataAsync(
                GetClientId(),
                jobId,
                Request.Headers["PlayerName"].ToString(),
                Request.Headers["MapName"].ToString(),
                int.Parse(Request.Headers["Duration"].ToString()),
                cancellationToken);

            return FromMutationResult(result);
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("finish-rendering")]
        public async Task<IActionResult> FinishRendering([FromQuery(Name = "job-id")] int jobId, CancellationToken cancellationToken)
        {
            var result = await renderService.FinishRenderingAsync(GetClientId(), jobId, cancellationToken);
            return FromMutationResult(result);
        }

        [Authorize(Roles = "sosubot")]
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelRender([FromQuery(Name = "job-id")] int jobId, CancellationToken cancellationToken)
        {
            var ok = await renderService.CancelRenderAsync(jobId, cancellationToken);
            return ok ? Ok() : NotFound();
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("get-next-render-job")]
        public async Task<IActionResult> GetNextRenderJob(CancellationToken cancellationToken)
        {
            var result = await renderService.GetNextRenderJobAsync(GetClientId(), cancellationToken);
            return result.Status switch
            {
                RenderJobAssignmentStatus.Assigned => Ok(result.Job),
                RenderJobAssignmentStatus.RendererOffline => RendererHeartbeatRequired(),
                RenderJobAssignmentStatus.RendererBusy => Conflict(new { message = "Renderer is already assigned an active render job.", jobId = result.CurrentJobId }),
                RenderJobAssignmentStatus.NoQueuedJobs => NotFound(),
                RenderJobAssignmentStatus.HigherPriorityRendererAvailable => Conflict(new
                {
                    message = "Another actively available renderer currently has higher priority for the next queued render job.",
                    nextRendererId = result.HigherPriorityRendererId,
                    nextRendererPerformancePoints = result.HigherPriorityRendererPerformancePoints
                }),
                _ => StatusCode(500)
            };
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("download-replay")]
        public async Task<IActionResult> DownloadReplay([FromQuery(Name = "job-id")] int jobId, CancellationToken cancellationToken)
        {
            var renderer = await renderService.GetOnlineRendererAsync(GetClientId(), cancellationToken);
            if (renderer == null)
            {
                return RendererHeartbeatRequired();
            }

            var renderJob = await renderService.GetRenderJobInfoAsync(jobId, cancellationToken);
            if (renderJob == null)
            {
                return NotFound();
            }

            logger.LogInformation("JobId: {JobId}. Replay downloaded", renderJob.JobId);
            return PhysicalFile(renderJob.ReplayPath, "application/octet-stream", $"replay_{jobId}.osr");
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("failure")]
        public async Task<IActionResult> Failure([FromQuery(Name = "job-id")] int jobId, [FromQuery(Name = "reason")] string failureReason, [FromQuery] bool rerender = true, CancellationToken cancellationToken = default)
        {
            var result = await renderService.FailRenderAsync(GetClientId(), jobId, failureReason, rerender, cancellationToken);
            return FromMutationResult(result, forbidOnMissingAssignment: true);
        }

        [Authorize(Roles = "sosubot")]
        [HttpPost("queue-replay")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(67108864)]
        [RequestFormLimits(MultipartBodyLengthLimit = 67108864)]
        public async Task<IActionResult> QueueReplay(
            [FromForm(Name = "file")] IFormFile? file,
            [FromForm(Name = "config")] string configAsStringJson,
            [FromHeader(Name = "Requested-By")] string requestedBy,
            CancellationToken cancellationToken)
        {
            if (file is not null && file.Length > 0 &&
                !Path.GetExtension(file.FileName).Equals(".osr", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Invalid replay file type.");
                return BadRequest("Invalid replay file type.");
            }

            try
            {
                var result = await renderService.QueueReplayAsync(file, configAsStringJson, requestedBy, cancellationToken);
                if (result == null)
                {
                    return BadRequest("Invalid render config.");
                }

                return Accepted(new { jobId = result.JobId, status = result.Status });
            }
            catch (InvalidOperationException ex) when (ex.Message == "Invalid skin file type.")
            {
                logger.LogWarning("Invalid skin file type.");
                return BadRequest("Invalid skin file type.");
            }
            catch (FileNotFoundException)
            {
                logger.LogWarning("You should firstly upload this skin");
                return BadRequest("You should firstly upload this skin");
            }
        }

        [Authorize(Roles = "sosubot-renderer")]
        [HttpPost("upload-replay-videofile")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(99614720)]
        [RequestFormLimits(MultipartBodyLengthLimit = 99614720)]
        public async Task<IActionResult> UploadReplayVideofile(
            [FromForm] IFormFile file,
            [FromQuery(Name = "job-id")] int jobId,
            [FromQuery(Name = "chunk-index")] int chunkIndex,
            [FromQuery(Name = "total-chunks")] int totalChunks,
            CancellationToken cancellationToken)
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

            var result = await renderService.UploadReplayVideofileAsync(Request, GetClientId(), file, jobId, chunkIndex, totalChunks, cancellationToken);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok();
        }

        [HttpGet("get-online-renderers")]
        public async Task<IActionResult> GetOnlineRenderers(CancellationToken cancellationToken)
            => Ok(await renderService.GetOnlineRenderersAsync(cancellationToken));

        [HttpGet("get-waitqueue-length")]
        public async Task<IActionResult> GetWaitqueueLength([FromQuery(Name = "job-id")] int jobId, CancellationToken cancellationToken)
        {
            var waitJobs = await renderService.GetWaitqueueLengthAsync(jobId, cancellationToken);
            return waitJobs.HasValue ? Ok(waitJobs.Value) : NotFound();
        }

        [HttpPost("get-render-job-info")]
        public async Task<IActionResult> GetRenderJobInfo([FromQuery(Name = "job-id")] int jobId, CancellationToken cancellationToken)
        {
            var renderJob = await renderService.GetRenderJobInfoAsync(jobId, cancellationToken);
            return renderJob == null ? NotFound() : Ok(renderJob);
        }
    }
}
