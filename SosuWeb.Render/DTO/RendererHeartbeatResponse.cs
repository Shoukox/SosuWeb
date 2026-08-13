using System.Text.Json.Serialization;

namespace SosuWeb.Render.DTO;

public sealed record RendererHeartbeatResponse(
    [property: JsonPropertyName("updateRequired")] bool UpdateRequired,
    [property: JsonPropertyName("latestVersion")] string? LatestVersion);
