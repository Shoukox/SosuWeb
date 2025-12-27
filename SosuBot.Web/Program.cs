using Microsoft.Extensions.FileProviders;
using SosuBot.Web.Constants;
using SosuBot.Web.Hubs;
using SosuBot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_073_741_824; // 1 GB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30);
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHostedService<ConfigureRabbitMqBackgroundService>();
builder.Services.AddSingleton<RabbitMqService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

if (!Directory.Exists(FilePathConstants.ReplaysPath))
{
    Directory.CreateDirectory(FilePathConstants.ReplaysPath);
}
if (!Directory.Exists(FilePathConstants.VideoPath))
{
    Directory.CreateDirectory(FilePathConstants.VideoPath);
}

app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = new PhysicalFileProvider(FilePathConstants.VideoPath),
    RequestPath = "/Videos"
});

app.UseDirectoryBrowser(new DirectoryBrowserOptions()
{
    FileProvider = new PhysicalFileProvider(FilePathConstants.VideoPath),
    RequestPath = "/Videos",
});

app.UseRouting();

app.UseAuthorization();

app.MapHub<RenderJobHub>("/render-job-hub");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();