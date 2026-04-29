using Microsoft.AspNetCore.Mvc;

namespace SosuWeb.Render.Controllers
{
    [Route("/watch")]
    public class WatchController : Controller
    {
        [HttpGet("{fileName}")]
        public IActionResult Index(string fileName)
        {
            return RedirectToAction("GetVideo", "Videos", new { fileName });
        }
    }
}
