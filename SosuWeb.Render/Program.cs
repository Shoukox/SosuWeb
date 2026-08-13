using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using SosuWeb.Database;
using SosuWeb.Render.Logging;
using SosuWeb.Render.Services;
using Npgsql;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<VideoService>();
builder.Services.AddSingleton<SkinService>();
builder.Services.AddSingleton<ThumbnailService>();
builder.Services.Configure<ClientRendererVersionOptions>(
    builder.Configuration.GetSection("ClientRenderer"));
builder.Services.AddScoped<RenderService>();
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
string configuredConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString);
string? databasePasswordFile = builder.Configuration["Database:PasswordFile"];
if (!string.IsNullOrWhiteSpace(databasePasswordFile))
{
    if (!File.Exists(databasePasswordFile))
        throw new FileNotFoundException("PostgreSQL password file was not found.", databasePasswordFile);

    connectionStringBuilder.Password = File.ReadAllText(databasePasswordFile).Trim();
}

string connectionString = connectionStringBuilder.ConnectionString;
Console.WriteLine(
    $"Using PostgreSQL at {connectionStringBuilder.Host}:{connectionStringBuilder.Port}/" +
    $"{connectionStringBuilder.Database} as {connectionStringBuilder.Username}");
builder.Services.AddDbContextPool<DatabaseContext>(options => options.UseNpgsql(connectionString)
        .ConfigureWarnings(m => m.Ignore(RelationalEventId.PendingModelChangesWarning)));
builder.Services.AddSingleton<IDistributedLockProvider>(_ => new PostgresDistributedSynchronizationProvider(connectionString));

// Build the app
var app = builder.Build();
bool migrateOnly = builder.Configuration.GetValue("Database:MigrateOnly", false);
bool migrateOnStartup = builder.Configuration.GetValue("Database:MigrateOnStartup", true);
if (migrateOnly || migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    db.Database.Migrate();
}

if (migrateOnly)
{
    Console.WriteLine("Database migrations completed; exiting migration-only mode.");
    return;
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
