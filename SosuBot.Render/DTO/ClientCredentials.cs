using System.Text.Json.Serialization;

namespace SosuBot.Render.DTO
{
    public record ClientCredentials
    {
        [JsonPropertyName("client-id")]
        public int ClientId { get; set; }

        [JsonPropertyName("client-secret")]
        public string ClientSecret { get; set; }
    }
}
