using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuBot.Database.Models
{
    public record Renderer
    {
        /// <summary>
        /// Client Id
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int RendererId { get; set; }
        public bool IsOnline { get; set; } = false;
        public DateTime LastSeen { get; set; } = DateTime.MinValue;
        public long BytesRendered { get; set; } = 0;
        public string UsedGPU { get; set; } = "undefined";
        public string UsedCPU { get; set; } = "undefined";
        public bool EncodingWithCPU { get; set; } = false;
        public List<RenderJob> CompletedJobs { get; set; } = new();
    }
}
