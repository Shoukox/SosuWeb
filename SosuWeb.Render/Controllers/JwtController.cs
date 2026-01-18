using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SosuWeb.Database;
using SosuWeb.Database.Models;
using SosuWeb.Render.DTO;
using SosuWeb.Render.Services;

namespace SosuWeb.Render.Controllers
{
    [ApiController]
    [Route("/jwt")]
    public class JwtController(JwtTokenService jwtService, DatabaseContext rendererContext, ILogger<JwtController> logger, IConfiguration conf) : ControllerBase
    {
        [HttpPost("")]
        public async Task<IActionResult> CreateToken([FromBody] ClientCredentials clientCredentials)
        {
            if (clientCredentials.GrantType != "client_credentials")
            {
                return Forbid("grant_type should be client_credentials");
            }
            if (clientCredentials.Scope != "renderer")
            {
                return Forbid("scope should be renderer");
            }

            int clientId = clientCredentials.ClientId;
            var rendererCredentials = rendererContext.RendererCredentials.FirstOrDefault(m => m.ClientId == clientId);
            if (rendererCredentials == null)
            {
                return Forbid();
            }

            var hasher = new PasswordHasher<RendererCredentials>();
            var clientSecretAuthResult = hasher.VerifyHashedPassword(rendererCredentials, rendererCredentials.ClientSecretHash, clientCredentials.ClientSecret);
            if (clientSecretAuthResult == PasswordVerificationResult.Failed)
            {
                return Forbid();
            }
            else if (clientSecretAuthResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                rendererCredentials.ClientSecretHash = hasher.HashPassword(rendererCredentials, clientCredentials.ClientSecret);
                await rendererContext.SaveChangesAsync();
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
                // duplicate
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                return StatusCode(500);
            }

            var jwtToken = jwtService.CreateToken(name: "todo", clientId: clientId);
            return Ok(
                new
                {
                    access_token = jwtToken,
                    token_type = "Bearer",
                    expires_in = conf.GetValue<int>("Jwt:ExpirationInMinutes"),
                }
            );
        }

#if DEBUG
        /// <summary>
        /// ONLY IN DEBUG. For convenient credentials generation
        /// </summary>
        /// <param name="clientId">client id</param>
        /// <returns></returns>
        [HttpGet("generate-credentials")]
        public async Task<IActionResult> GenerateCredentials([FromQuery(Name = "client_id")] int clientId)
        {
            var rendererCredentials = rendererContext.RendererCredentials.Add(new()
            {
                ClientId = clientId,
                ClientSecretHash = "", // to be set further
                CreatedAt = DateTime.UtcNow,
            });

            var hasher = new PasswordHasher<RendererCredentials>();
            var generatedClientSecret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            rendererCredentials.Entity.ClientSecretHash = hasher.HashPassword(rendererCredentials.Entity, generatedClientSecret);
            await rendererContext.SaveChangesAsync();

            return Ok(new
            {
                client_id = rendererCredentials.Entity.ClientId,
                client_secret = generatedClientSecret,
            });
        }

        /// <summary>
        /// ONLY IN DEBUG. Revoke client_secret
        /// </summary>
        /// <param name="clientId">client id</param>
        /// <returns></returns>
        [HttpGet("revoke-client-secret")]
        public async Task<IActionResult> RevokeClientSecret([FromQuery(Name = "client_id")] int clientId)
        {
            if (rendererContext.RendererCredentials.FirstOrDefault(m => m.ClientId == clientId) is not { } rendererCredentials)
            {
                return NotFound();
            }

            var hasher = new PasswordHasher<RendererCredentials>();
            var generatedClientSecret = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            rendererCredentials.ClientSecretHash = hasher.HashPassword(rendererCredentials, generatedClientSecret);
            await rendererContext.SaveChangesAsync();

            return Ok(new
            {
                client_id = rendererCredentials.ClientId,
                client_secret = generatedClientSecret,
            });
        }
#endif
    }
}
