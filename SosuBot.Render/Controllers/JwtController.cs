using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Render.DTO;
using SosuBot.Render.Services;
using System.Security.Cryptography;
using System.Text;

namespace SosuBot.Render.Controllers
{
    [ApiController]
    [Route("/jwt")]
    public class JwtController(JwtTokenService jwtService, DatabaseContext rendererContext, ILogger<JwtController> logger) : ControllerBase
    {
        [HttpPost("")]
        public async Task<IActionResult> CreateToken([FromBody] ClientCredentials clientCredentials)
        {
            int clientId = clientCredentials.ClientId;
            var rendererCredentials = rendererContext.RendererCredentials.FirstOrDefault(m => m.ClientId == clientId);
            if (rendererCredentials == null)
            {
                return Forbid();
            }

            var computedClientSecretHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(clientCredentials.ClientSecret + rendererCredentials.ClientSecretSalt))).ToLower();
            if(computedClientSecretHash != rendererCredentials.ClientSecretHash)
            {
                return Forbid();
            }

            try
            {
                var renderer = new Renderer()
                {
                    RendererId = clientId,
                    LastSeen = DateTime.UtcNow,
                    IsOnline = true
                };
                await rendererContext.Renderers.AddAsync(renderer);
                await rendererContext.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pe && pe.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // duplicate, do nothing
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                return StatusCode(500);
            }

            var jwtToken = jwtService.CreateToken(name: "todo", clientId: clientId);
            return Ok(jwtToken);
        }
    }
}
