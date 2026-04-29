using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuWeb.Database.Models
{
    public record OAuthCredentials
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public required int ClientId { get; set; }
        public string ClientSecretHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.MinValue;
    }
}
