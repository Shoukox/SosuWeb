using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using SosuBot.Database;
using SosuBot.Render.Services;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHostedService<RendererOfflineService>();

// Load jwt certificates
var rsaPublic = RSA.Create();
rsaPublic.ImportFromPem(File.ReadAllText("jwt/jwt_rsa_pub.key"));

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(10),

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsaPublic),

            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });

// Authorization
builder.Services.AddAuthorization();

// Database
var pwFile = Path.Combine(AppContext.BaseDirectory, ".secrets",".db_password");
var dbPassword = File.ReadAllText(pwFile).Trim();
var connectionString = string.Format(builder.Configuration.GetConnectionString("Postgres")!, dbPassword);
Console.WriteLine($"Using the following connection string: {connectionString}");
builder.Services.AddDbContextPool<DatabaseContext>(options => options.UseNpgsql(connectionString)
        .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));
builder.Services.AddSingleton<IDistributedLockProvider>(_ => new PostgresDistributedSynchronizationProvider(connectionString));

// Build the app
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
