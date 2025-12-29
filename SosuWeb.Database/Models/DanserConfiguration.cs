using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SosuWeb.Database.Models
{
    public record DanserConfiguration
    {
        public int VideoWidth { get; set; } = 1280;
        public int VideoHeight { get; set; } = 720;
        public string Encoder { get; set; } = "h264_nvenc";
        public string SkinName { get; set; } = "default";
        public double GeneralVolume { get; set; } = 0.5;
        public double MusicVolume { get; set; } = 0.5;
        public double SampleVolume { get; set; } = 0.5;
        public bool HitErrorMeter { get; set; } = false;
        public bool AimErrorMeter { get; set; } = false;
        public bool HPBar { get; set; } = true;
        public bool ShowPP { get; set; } = false;
        public bool HitCounter { get; set; } = false;
        public bool IgnoreFailsInReplays { get; set; } = false;
        public bool Video { get; set; } = false;
        public bool Storyboard { get; set; } = false;
        public bool Mods { get; set; } = false;
        public bool KeyOverlay { get; set; } = false;
        public bool Combo { get; set; } = false;
    }
}
