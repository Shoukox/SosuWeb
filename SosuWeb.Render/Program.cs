using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using SosuWeb.Database;
using SosuWeb.Render.Logging;
using SosuWeb.Render.Services;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<VideoService>();
builder.Services.AddSingleton<SkinService>();
builder.Services.AddHostedService<RendererOfflineService>();
builder.Services.AddHostedService<RendererStuckReplayResetService>();

// Data protection
string dpDirName = "dpkeys-sosuweb-render";
Directory.CreateDirectory(dpDirName);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpDirName))
    .SetApplicationName("SosuWeb.Render");

// Logging
var loggingFileName = "logs/{Date}.log";
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFile(loggingFileName, LogLevel.Warning);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>();

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
var connectionString = builder.Configuration.GetConnectionString("Postgres")!;
Console.WriteLine($"Using the following connection string: {connectionString}");
builder.Services.AddDbContextPool<DatabaseContext>(options => options.UseNpgsql(connectionString)
        .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));
builder.Services.AddSingleton<IDistributedLockProvider>(_ => new PostgresDistributedSynchronizationProvider(connectionString));

// Build the app
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    db.Database.Migrate();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
