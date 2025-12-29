using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace SosuWeb.Render.Services
{
    public class JwtTokenService
    {
        private readonly IConfiguration _conf;
        private readonly ILogger<JwtTokenService> _logger;
        private readonly RSA _rsaPrivate;

        public JwtTokenService(IConfiguration conf, ILogger<JwtTokenService> logger)
        {
            _rsaPrivate = RSA.Create();
            _rsaPrivate.ImportFromPem(
                File.ReadAllText("jwt/jwt_rsa.key"));
            _conf = conf;
            _logger = logger;
        }

        // todo create only if user has provided a valid client-secret
        public string CreateToken(string name, int clientId)
        {
            var claims = new Claim[]
            {
                new Claim("role", _conf["Jwt:Role"]!),
                new Claim("name", name),
                new Claim("client-id", $"{clientId}"),
            };

            var creds = new SigningCredentials(
                new RsaSecurityKey(_rsaPrivate),
                SecurityAlgorithms.RsaSha256);

            if(!int.TryParse(_conf["Jwt:ExpirationInMinutes"], out int expiration))
            {
                _logger.LogWarning("Wrong value for Jwt:ExpirationInMinutes in appsettings.json");
                expiration = 60;
            }
            var token = new JwtSecurityToken(
                issuer: _conf["Jwt:Issuer"],
                audience: _conf["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiration),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
