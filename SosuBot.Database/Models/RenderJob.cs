using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuBot.Database.Models;

public record RenderJob
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int JobId { get; set; }
    public string ReplayPath { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public string RequestedBy { get; set; } = null!;
    public int RenderingBy { get; set; } = -1;
    public double ProgressPercent { get; set; } = 0; // 0.00 ... 1.00
    public bool IsComplete { get; set; } = false;
}