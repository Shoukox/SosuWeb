using System.Text.Json.Serialization;

namespace SosuWeb.Render.DTO
{
    public record ClientCredentials
    {
        [JsonPropertyName("client_id")] public required int ClientId { get; set; }

        [JsonPropertyName("client_secret")] public required string ClientSecret { get; set; }

        [JsonPropertyName("grant_type")] public required string GrantType { get; set; }

        [JsonPropertyName("scope")] public required string Scope { get; set; }
    }
}
