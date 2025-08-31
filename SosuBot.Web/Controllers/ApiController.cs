using Microsoft.AspNetCore.Mvc;
using NUnit.Framework.Constraints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using SosuBot.Logging;
using SosuBot.PerformanceCalculator;

namespace SosuBot.Web.Controllers;

[ApiController]
public class ApiController : ControllerBase
{
    private static ILogger Logger = ApplicationLogging.CreateLogger(nameof(ApiController));

    [HttpGet("/getstdpp")]
    public async Task<IActionResult> GetStdPp(
        [FromQuery(Name = "key")] string? key,
        [FromQuery(Name = "beatmap_id")] int beatmapId,
        [FromQuery(Name = "accuracy")] double? accuracy,
        [FromQuery(Name = "great")] int? great,
        [FromQuery(Name = "ok")] int? ok,
        [FromQuery(Name = "meh")] int? meh,
        [FromQuery(Name = "slider_tails_hit")] int? sliderTailsHit,
        [FromQuery(Name = "miss")] int? miss,
        [FromQuery(Name = "score_max_combo")] int? scoreMaxCombo,
        [FromQuery(Name = "mods")] string? mods = null)
    {
        if (key == null || key != "")
        {
            return Unauthorized();
        }
        if (accuracy == null && great == null && ok == null && meh == null && miss == null && sliderTailsHit == null)
        {
            return BadRequest("Either an accuracy or a score statistics (great, ok, meh, miss, slider_tails_hit) are needed");
        }
        if (accuracy != null && (great != null || ok != null || meh != null || miss != null || sliderTailsHit != null))
        {
            return BadRequest("Either an accuracy or a score statistics (great, ok, meh, miss, slider_tails_hit) are needed");
        }

        if (accuracy == null && (great == null || ok == null || meh == null || miss == null || sliderTailsHit == null))
        {
            return BadRequest("In case if an accuracy was not provided, the score statistics great, ok, meh, miss, slider_tails_hit are all required. ");
        }
        
        try
        {
            PPCalculator ppCalculator = new PPCalculator();

            Dictionary<HitResult, int>? statistics = null;
            if (accuracy == null)
            {
                statistics = new();
                statistics.Add(HitResult.Great, great.GetValueOrDefault());
                statistics.Add(HitResult.Ok, ok.GetValueOrDefault());
                statistics.Add(HitResult.Meh, meh.GetValueOrDefault());
                statistics.Add(HitResult.Miss, miss.GetValueOrDefault());
                
                if (sliderTailsHit.HasValue)
                {
                    statistics.Add(HitResult.SliderTailHit, sliderTailsHit!.Value);
                }
            }
            
            PPCalculationResult pp = await ppCalculator.CalculatePpAsync(beatmapId,
                accuracy: accuracy,
                scoreMaxCombo: scoreMaxCombo,
                scoreStatistics: statistics,
                scoreMods: ToMods(mods));
            
            return Ok(pp);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting PP stats");
            return BadRequest();
        }
    }

    private static readonly Mod[] AllOsuMods = typeof(OsuModNoFail).Assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
        .Where(t => typeof(Mod).IsAssignableFrom(t))
        .Where(t => t.Name.StartsWith("OsuMod"))
        .Select(t => (Mod)Activator.CreateInstance(t)!)
        .ToArray()!;

    private static Mod[] ToMods(string? text)
    {
        if (text == null)
        {
            return [];
        }
        
        text = text.Trim().ToUpperInvariant();

        var startFrom = 0;
        if (!char.IsAsciiLetter(text[0])) startFrom = 1;
        
        var mods = new List<Mod>();
        for (var i = startFrom; i < text.Length; i += 2)
        {
            var currentMod = AllOsuMods.FirstOrDefault(m => m.Acronym.ToUpperInvariant() == text.Substring(i, 2));
            if (currentMod == null) continue;
            mods.Add(currentMod);
        }

        return mods.ToArray();
    }
}