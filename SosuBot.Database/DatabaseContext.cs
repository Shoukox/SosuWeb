using Microsoft.EntityFrameworkCore;
using SosuBot.Database.Models;

namespace SosuBot.Database
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        public DbSet<RenderJob> RenderJobs { get; set; }
        public DbSet<Renderer> Renderers { get; set; }
        public DbSet<RendererCredentials> RendererCredentials { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RendererCredentials>().HasData(new RendererCredentials
            {
                ClientId = 1234,
                ClientSecretHash = "9a9b4043565915eea98c07ff06bcb15e615a2477a4b04fbdd8645a7a4da531027c740aa9a865f34df497a1d0665658b1d9757ebe3347b33c650175a0ba2a2eb4",
                ClientSecretSalt = "sosubot_renderer1234",
                // client_secret = fVk2CsmhfACz

            });
        }
    }
}
