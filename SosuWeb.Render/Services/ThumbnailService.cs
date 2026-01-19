using SosuWeb.Database.Models;
using System.Buffers.Text;
using System.Text;

namespace SosuWeb.Render.Services
{
    public class ThumbnailService
    {
        public string GetThumbnailFileName(int jobId, DateTime requestedAt)
        {
            return Base64Url.EncodeToString(Encoding.ASCII.GetBytes($"{jobId}_{requestedAt.ToFileTimeUtc()}")) + ".png";
        }
    }
}
