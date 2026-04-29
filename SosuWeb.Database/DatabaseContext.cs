using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SosuWeb.Database.Models;
using System.Text.Json;

namespace SosuWeb.Database
{
    public class DatabaseContext : IdentityDbContext<ApplicationUser>
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        public DbSet<RenderJob> RenderJobs { get; set; }
        public DbSet<Renderer> Renderers { get; set; }
        public DbSet<OAuthCredentials> OAuthCredentials { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var jsonConfig = new JsonSerializerOptions() { WriteIndented = false };

            // Convert render settings
            var renderSettingsComparer = new ValueComparer<RenderSettings>(
                (l, r) => JsonSerializer.Serialize(l) == JsonSerializer.Serialize(r),
                v => JsonSerializer.Serialize(v).GetHashCode(),
                v => JsonSerializer.Deserialize<RenderSettings>(
                        JsonSerializer.Serialize(v))!
            );
            var renderSettingsConverter = new ValueConverter<RenderSettings, string>(
                v => JsonSerializer.Serialize(v, jsonConfig),
                v => JsonSerializer.Deserialize<RenderSettings>(v)!);
            modelBuilder.Entity<RenderJob>()
                .Property(e => e.RenderSettings)
                .HasConversion(renderSettingsConverter, renderSettingsComparer)
                .HasColumnType("jsonb");
        }
    }
}
