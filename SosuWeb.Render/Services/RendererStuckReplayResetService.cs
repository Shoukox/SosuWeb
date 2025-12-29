using SosuWeb.Database;

namespace SosuWeb.Render.Services
{
    public class RendererStuckReplayResetService : BackgroundService
    {
        private readonly ILogger<RendererStuckReplayResetService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public const int RenderTimeoutInSeconds = 180;

        public RendererStuckReplayResetService(
            IServiceProvider serviceProvider,
            ILogger<RendererStuckReplayResetService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    var timeout = DateTime.UtcNow.AddSeconds(-RenderTimeoutInSeconds);

                    var stuckRenderJobs = db.RenderJobs
                        .Where(r =>
                            r.RenderingBy != -1 &&
                            !r.IsComplete &&
                            r.RenderingLastUpdate <= timeout &&
                            string.IsNullOrEmpty(r.FailureReason)
                        );

                    foreach (var renderJob in stuckRenderJobs)
                    {
                        _logger.LogWarning(
                            "Resetting stuck replay {JobId} (Renderer={RenderingBy}, StartedAt={RenderingStartedAt})",
                            renderJob.JobId,
                            renderJob.RenderingBy,
                            renderJob.RenderingStartedAt
                        );

                        renderJob.RenderingBy = -1;
                        renderJob.IsComplete = false;
                        renderJob.RenderingStartedAt = default;
                        renderJob.RenderingLastUpdate = default;
                        renderJob.ProgressPercent = 0;
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in RendererStuckReplayResetService");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
