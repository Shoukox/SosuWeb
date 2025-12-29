using SosuWeb.Database.Models;
using System.Text;

namespace SosuWeb.Render.Services
{
    public class SkinService
    {
        public string SkinFileNameToHex(string skinOriginFileName)
        {
            return Convert.ToHexString(Encoding.ASCII.GetBytes(skinOriginFileName)) + ".osk";
        }

        public string SkinFileNameFromHex(string skinFileNameAsHex)
        {
            if(skinFileNameAsHex.EndsWith(".osk"))
            {
                skinFileNameAsHex = skinFileNameAsHex.Substring(0, skinFileNameAsHex.Length - 4);
            }
            return Encoding.ASCII.GetString(Convert.FromHexString(skinFileNameAsHex));
        }
    }
}
