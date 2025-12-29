using SosuWeb.Database.Models;
using System.Text;

namespace SosuWeb.Render.Services
{
    public class VideoService
    {
        public string GetReplayVideoFileName(int jobId, DateTime requestedAt)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes($"{jobId}_{requestedAt.ToFileTimeUtc()}")) + ".mp4";
        }
    }
}
