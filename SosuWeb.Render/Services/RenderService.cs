using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using SosuWeb.Database;
using SosuWeb.Database.Models;
using SosuWeb.Render.Controllers;
using System.Text.Json;

namespace SosuWeb.Render.Services;

public enum RenderJobAssignmentStatus
{
    Assigned,
    RendererOffline,
    RendererBusy,
    NoQueuedJobs,
    HigherPriorityRendererAvailable,
}

public sealed record RenderJobAssignmentResult(
    RenderJobAssignmentStatus Status,
    RenderJob? Job = null,
    int? CurrentJobId = null,
    int? HigherPriorityRendererId = null,
    int? HigherPriorityRendererPerformancePoints = null);

public enum RenderJobMutationStatus
{
    Success,
    RendererOffline,
    JobNotFound,
    Forbidden,
    RendererJobMismatch,
}

public sealed record RenderJobMutationResult(RenderJobMutationStatus Status, RenderJob? Job = null);

public sealed record QueueReplayResult(int JobId, string Status);

public sealed record UploadReplayVideoResult(bool Success, string? ErrorMessage = null, bool IsNotFound = false);

public class RenderService(
    DatabaseContext db,
    ILogger<RenderService> logger,
    IDistributedLockProvider synchronizationProvider,
    VideoService videoService,
    SkinService skinService)
{
    private static readonly TimeSpan RendererQueueFreshnessWindow = TimeSpan.FromSeconds(10);

    public async Task<Renderer?> GetRendererAsync(int clientId, CancellationToken cancellationToken = default)
        => await db.Renderers.FirstOrDefaultAsync(m => m.RendererId == clientId, cancellationToken);

    public async Task<Renderer?> GetOnlineRendererAsync(int clientId, CancellationToken cancellationToken = default)
        => await db.Renderers.FirstOrDefaultAsync(m => m.RendererId == clientId && m.IsOnline, cancellationToken);

    public async Task<bool> HeartbeatAsync(int clientId, CancellationToken cancellationToken = default)
    {
        var renderer = await GetRendererAsync(clientId, cancellationToken);
        if (renderer == null)
        {
            logger.LogWarning("Heartbeat error, clientId: {ClientId}", clientId);
            return false;
        }

        renderer.LastSeen = DateTime.UtcNow;
        renderer.IsOnline = true;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Heartbeat received from renderer {RendererId}", clientId);
        return true;
    }

    public async Task<RenderJobMutationResult> ReportRenderingProgressAsync(int clientId, int jobId, double progress, CancellationToken cancellationToken = default)
    {
        var assignment = await GetAssignedJobAsync(clientId, jobId, cancellationToken);
        if (assignment.Status != RenderJobMutationStatus.Success)
        {
            return assignment;
        }

        assignment.Job!.ProgressPercent = progress;
        assignment.Job.RenderingLastUpdate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("JobId: {JobId}, progress: {Progress}", assignment.Job.JobId, progress);
        return assignment;
    }

    public async Task<RenderJobMutationResult> SetRenderJobMetadataAsync(int clientId, int jobId, string? playerName, string? mapName, int duration, CancellationToken cancellationToken = default)
    {
        var assignment = await GetAssignedJobAsync(clientId, jobId, cancellationToken);
        if (assignment.Status != RenderJobMutationStatus.Success)
        {
            return assignment;
        }

        assignment.Job!.RenderingLastUpdate = DateTime.UtcNow;
        assignment.Job.PlayerName = string.IsNullOrWhiteSpace(playerName) ? assignment.Job.PlayerName : playerName;
        assignment.Job.MapName = string.IsNullOrWhiteSpace(mapName) ? assignment.Job.MapName : mapName;
        assignment.Job.VideoDuration = duration;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("JobId: {JobId}. Metadata: player name = {PlayerName}, map name = {MapName}", assignment.Job.JobId, assignment.Job.PlayerName, assignment.Job.MapName);
        return assignment;
    }

    public async Task<RenderJobMutationResult> FinishRenderingAsync(int clientId, int jobId, CancellationToken cancellationToken = default)
    {
        var assignment = await GetAssignedJobAsync(clientId, jobId, cancellationToken);
        if (assignment.Status != RenderJobMutationStatus.Success)
        {
            return assignment;
        }

        var renderer = await GetOnlineRendererAsync(clientId, cancellationToken);
        if (renderer == null)
        {
            return new(RenderJobMutationStatus.RendererOffline);
        }

        assignment.Job!.RenderingLastUpdate = DateTime.UtcNow;
        assignment.Job.IsComplete = true;
        assignment.Job.IsSuccess = true;
        ClearRendererCurrentJob(renderer);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("JobId: {JobId}. Completed", assignment.Job.JobId);
        return assignment;
    }

    public async Task<bool> CancelRenderAsync(int jobId, CancellationToken cancellationToken = default)
    {
        var renderJob = await db.RenderJobs.FirstOrDefaultAsync(r => r.JobId == jobId && !r.IsComplete, cancellationToken);
        if (renderJob == null)
        {
            return false;
        }

        renderJob.RenderingLastUpdate = DateTime.UtcNow;
        renderJob.IsComplete = true;
        renderJob.IsSuccess = false;
        renderJob.FailureReason = "Cancelled";

        if (renderJob.RenderingBy != -1)
        {
            var renderer = await GetRendererAsync(renderJob.RenderingBy, cancellationToken);
            if (renderer != null)
            {
                ClearRendererCurrentJob(renderer);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("JobId: {JobId}. Cancelled", renderJob.JobId);
        return true;
    }

    public async Task<RenderJobAssignmentResult> GetNextRenderJobAsync(int clientId, CancellationToken cancellationToken = default)
    {
        await using var @lock = await synchronizationProvider.AcquireLockAsync("render-job-lock", cancellationToken: cancellationToken);

        var renderer = await GetOnlineRendererAsync(clientId, cancellationToken);
        if (renderer == null)
        {
            return new(RenderJobAssignmentStatus.RendererOffline);
        }

        if (renderer.IsRendering && renderer.CurrentJobId != -1)
        {
            return new(RenderJobAssignmentStatus.RendererBusy, CurrentJobId: renderer.CurrentJobId);
        }

        var freeJob = await db.RenderJobs
            .OrderBy(m => m.JobId)
            .FirstOrDefaultAsync(m => !m.IsComplete && m.RenderingBy == -1, cancellationToken);
        if (freeJob == null)
        {
            return new(RenderJobAssignmentStatus.NoQueuedJobs);
        }

        var now = DateTime.UtcNow;
        var priorityRenderers = await db.Renderers
            .Where(r => r.IsOnline && !r.IsRendering && r.LastSeen >= now - RendererQueueFreshnessWindow)
            .OrderByDescending(r => r.PerformancePoints)
            .ThenByDescending(r => r.LastSeen)
            .ThenBy(r => r.RendererId)
            .Select(r => new { r.RendererId, r.PerformancePoints })
            .ToListAsync(cancellationToken);

        if (priorityRenderers.Count > 0 && priorityRenderers[0].RendererId != renderer.RendererId)
        {
            return new(
                RenderJobAssignmentStatus.HigherPriorityRendererAvailable,
                HigherPriorityRendererId: priorityRenderers[0].RendererId,
                HigherPriorityRendererPerformancePoints: priorityRenderers[0].PerformancePoints);
        }

        freeJob.RenderingBy = renderer.RendererId;
        freeJob.RenderingStartedAt = now;
        freeJob.RenderingLastUpdate = now;
        renderer.IsRendering = true;
        renderer.CurrentJobId = freeJob.JobId;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Assigning JobId: {JobId} to RendererId: {RendererId} (PerformancePoints: {PerformancePoints})", freeJob.JobId, renderer.RendererId, renderer.PerformancePoints);
        return new(RenderJobAssignmentStatus.Assigned, Job: freeJob);
    }

    public async Task<RenderJobMutationResult> FailRenderAsync(int clientId, int jobId, string failureReason, bool rerender = true, CancellationToken cancellationToken = default)
    {
        var renderer = await GetOnlineRendererAsync(clientId, cancellationToken);
        if (renderer == null)
        {
            return new(RenderJobMutationStatus.RendererOffline);
        }

        var renderJob = await db.RenderJobs.FirstOrDefaultAsync(r =>
            r.JobId == jobId
            && !r.IsComplete
            && r.RenderingBy == renderer.RendererId, cancellationToken);
        if (renderJob == null)
        {
            return new(RenderJobMutationStatus.Forbidden);
        }

        if (!renderer.IsRendering || renderer.CurrentJobId != jobId)
        {
            return new(RenderJobMutationStatus.RendererJobMismatch);
        }

        renderJob.IsSuccess = false;
        renderJob.FailureReason = failureReason;
        renderJob.IsComplete = !rerender;
        renderJob.RenderingBy = rerender ? -1 : renderJob.RenderingBy;
        renderJob.RenderingStartedAt = default;
        renderJob.RenderingLastUpdate = default;
        renderJob.ProgressPercent = 0;
        ClearRendererCurrentJob(renderer);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("JobId: {JobId}. Failed: {FailureReason}", renderJob.JobId, failureReason);
        return new(RenderJobMutationStatus.Success, renderJob);
    }

    public async Task<QueueReplayResult?> QueueReplayAsync(IFormFile? file, string configAsStringJson, string requestedBy, CancellationToken cancellationToken = default)
    {
        var config = JsonSerializer.Deserialize<RenderSettings>(configAsStringJson);
        if (config == null)
        {
            return null;
        }

        if (!config.UseAutoPlay && (file is null || file.Length == 0))
        {
            return null;
        }

        if (config.UseAutoPlay && (!config.AutoBeatmapId.HasValue || config.AutoBeatmapId.Value <= 0))
        {
            return null;
        }

        if (config.SkinName != "default")
        {
            if (!Path.GetExtension(config.SkinName)!.Equals(".osk", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid skin file type.");
            }

            string skinsDirectoryPath = SkinsController.SkinsDir;
            string skinFileNameHex = skinService.SkinFileNameToHex(config.SkinName);
            string skinPath = Path.Combine(skinsDirectoryPath, skinFileNameHex);
            if (!File.Exists(skinPath))
            {
                throw new FileNotFoundException("You should firstly upload this skin", skinPath);
            }
        }

        var datetimeUtcNow = DateTime.UtcNow;
        string storagePath = string.Empty;
        if (file is not null && file.Length > 0)
        {
            var replayFileName = $"{datetimeUtcNow.ToFileTimeUtc()}.osr";
            var replayDirectoryPath = Path.Combine(AppContext.BaseDirectory, "replays");
            storagePath = Path.Combine(replayDirectoryPath, replayFileName);
            Directory.CreateDirectory(replayDirectoryPath);

            await using var stream = new FileStream(storagePath, FileMode.CreateNew);
            await file.CopyToAsync(stream, cancellationToken);
        }

        var renderJob = new RenderJob
        {
            ReplayPath = storagePath,
            RequestedAt = datetimeUtcNow,
            RequestedBy = requestedBy,
            RenderSettings = config
        };

        await db.RenderJobs.AddAsync(renderJob, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("New render job queued. JobId: {JobId}, RequestedBy: {RequestedBy}", renderJob.JobId, requestedBy);

        return new QueueReplayResult(renderJob.JobId, "queued");
    }

    public async Task<UploadReplayVideoResult> UploadReplayVideofileAsync(HttpRequest request, int clientId, IFormFile file, int jobId, int chunkIndex, int totalChunks, CancellationToken cancellationToken = default)
    {
        var renderer = await GetOnlineRendererAsync(clientId, cancellationToken);
        if (renderer == null)
        {
            return new UploadReplayVideoResult(false, "You should send a heartbeat");
        }

        var renderJob = await db.RenderJobs.FirstOrDefaultAsync(r => r.JobId == jobId, cancellationToken);
        if (renderJob == null)
        {
            return new UploadReplayVideoResult(false, IsNotFound: true);
        }

        const string chunkFileNameFormat = "{0}_part{1}.mp4";
        Directory.CreateDirectory(VideosController.VideosDir);
        var chunkPath = Path.Combine(VideosController.VideosDir, string.Format(chunkFileNameFormat, jobId, chunkIndex));

        await using (var fs = new FileStream(chunkPath, FileMode.Create))
        {
            await file.CopyToAsync(fs, cancellationToken);
        }

        logger.LogInformation("JobId: {JobId}. Got chunk {ChunkNumber}/{TotalChunks}", renderJob.JobId, chunkIndex + 1, totalChunks);

        if (chunkIndex + 1 == totalChunks)
        {
            var replayVideoFileName = videoService.GetReplayVideoFileName(renderJob.JobId, renderJob.RequestedAt);
            var finalFile = Path.Combine(VideosController.VideosDir, replayVideoFileName);
            await using var output = new FileStream(finalFile, FileMode.Create);
            for (int i = 0; i < totalChunks; i++)
            {
                var partPath = Path.Combine(VideosController.VideosDir, string.Format(chunkFileNameFormat, jobId, i));
                await using (var partStream = new FileStream(partPath, FileMode.Open))
                {
                    await partStream.CopyToAsync(output, cancellationToken);
                }
                File.Delete(partPath);
            }

            renderer.BytesRendered += output.Length;
            renderJob.VideoLocalPath = Path.GetFullPath(finalFile);
            renderJob.VideoUri = $"{request.Scheme}://{request.Host.ToString().Replace("localhost", "127.0.0.1")}{request.PathBase}/videos/{replayVideoFileName}";
            await db.SaveChangesAsync(cancellationToken);
        }

        return new UploadReplayVideoResult(true);
    }

    public async Task<List<object>> GetOnlineRenderersAsync(CancellationToken cancellationToken = default)
        => await db.Renderers
            .Where(r => r.IsOnline)
            .Select(r => (object)new
            {
                r.RendererId,
                r.LastSeen,
                r.RendererName,
                r.UsedGPU,
                r.IsRendering,
                r.CurrentJobId,
                r.PerformancePoints
            })
            .ToListAsync(cancellationToken);

    public async Task<int?> GetWaitqueueLengthAsync(int jobId, CancellationToken cancellationToken = default)
    {
        var renderJob = await db.RenderJobs.FirstOrDefaultAsync(r => r.JobId == jobId, cancellationToken);
        if (renderJob == null)
        {
            return null;
        }

        var activeJob = await db.RenderJobs
            .Where(r => r.RenderingBy != -1)
            .OrderByDescending(r => r.JobId)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeJob == null)
        {
            return Math.Max(0, (await db.RenderJobs.CountAsync(r => !r.IsComplete && r.RenderingBy == -1 && r.JobId < jobId, cancellationToken)));
        }

        return Math.Max(1, jobId - activeJob.JobId) - 1;
    }

    public async Task<RenderJob?> GetRenderJobInfoAsync(int jobId, CancellationToken cancellationToken = default)
        => await db.RenderJobs.FirstOrDefaultAsync(r => r.JobId == jobId, cancellationToken);

    private async Task<RenderJobMutationResult> GetAssignedJobAsync(int clientId, int jobId, CancellationToken cancellationToken)
    {
        var renderer = await GetOnlineRendererAsync(clientId, cancellationToken);
        if (renderer == null)
        {
            return new(RenderJobMutationStatus.RendererOffline);
        }

        var renderJob = await db.RenderJobs.FirstOrDefaultAsync(r =>
            r.JobId == jobId
            && r.RenderingBy == renderer.RendererId
            && !r.IsComplete, cancellationToken);
        if (renderJob == null)
        {
            return new(RenderJobMutationStatus.JobNotFound);
        }

        if (!renderer.IsRendering || renderer.CurrentJobId != jobId)
        {
            return new(RenderJobMutationStatus.RendererJobMismatch);
        }

        return new(RenderJobMutationStatus.Success, renderJob);
    }

    private static void ClearRendererCurrentJob(Renderer renderer)
    {
        renderer.IsRendering = false;
        renderer.CurrentJobId = -1;
    }
}
