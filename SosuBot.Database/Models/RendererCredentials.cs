using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuBot.Database.Models
{
    public record RendererCredentials
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public required int ClientId { get; set; }
        public string ClientSecretHash { get; set; } = string.Empty;
        public string ClientSecretSalt { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.MinValue;
    }
}
