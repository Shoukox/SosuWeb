using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuWeb.Database.Models
{
    public record Renderer
    {
        /// <summary>
        /// Client Id
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int RendererId { get; set; }
        public string RendererName { get; set; } = "undefined";
        public bool IsOnline { get; set; } = false;
        public DateTime LastSeen { get; set; } = DateTime.MinValue;
        public long BytesRendered { get; set; } = 0;
        public string UsedGPU { get; set; } = "undefined";
        public string UsedCPU { get; set; } = "undefined";
        public bool EncodingWithCPU { get; set; } = false;
        public bool IsRendering { get; set; } = false;
        public int CurrentJobId { get; set; } = -1;
        public int PerformancePoints { get; set; } = 0; // Calculated from the renderer's performance benchmarks, used for job assignment prioritization. Bigger number means higher priority.
        public List<RenderJob> CompletedJobs { get; set; } = new();
    }
}
