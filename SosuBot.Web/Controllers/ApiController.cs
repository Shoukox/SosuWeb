using Microsoft.AspNetCore.Mvc;
using SosuBot.Logging;
using SosuBot.PerformanceCalculator;

namespace SosuBot.Web.Controllers;

[ApiController]
public class ApiController : ControllerBase
{
    private static ILogger Logger = ApplicationLogging.CreateLogger(nameof(ApiController));
    
    [HttpGet("/getstdpp")]
    public async Task<IActionResult> GetStdPp(
        [FromQuery(Name = "beatmap_id")] int beatmapId,
        [FromQuery(Name = "great")] int great,
        [FromQuery(Name = "ok")] int ok,
        [FromQuery(Name = "meh")] int meh,
        [FromQuery(Name = "miss")] int miss)
    {
        try
        {
            PPCalculator ppCalculator = new PPCalculator();
            PPCalculationResult pp = await ppCalculator.CalculatePpAsync(beatmapId, great, ok, meh, miss);
            
            
            return Ok();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting PP stats");
            return BadRequest();
        }
    }
}