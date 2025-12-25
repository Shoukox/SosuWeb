using SosuBot.Database;

namespace SosuBot.Render.Services
{
    public class RendererOfflineService : BackgroundService
    {
        private readonly ILogger<RendererOfflineService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public const int TimeoutInSeconds = 60;

        public RendererOfflineService(IServiceProvider serviceProvider, ILogger<RendererOfflineService> logger)
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

                    var timeout = DateTime.UtcNow.AddSeconds(TimeoutInSeconds);
                    var inactiveRenderers = db.Renderers.Where(r => r.IsOnline && r.LastSeen < timeout);

                    foreach (var renderer in inactiveRenderers)
                    {
                        renderer.IsOnline = false;
                        _logger.LogInformation($"Renderer {renderer.RendererId} marked offline due to inactivity.");
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error in RendererOfflineService");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // run every 30s
            }
        }
    }
}
