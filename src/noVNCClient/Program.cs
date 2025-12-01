using noVNCClient.Middlewares;
using noVNCClient.Models;

namespace noVNCClient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddMemoryCache();

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthorization();

            // Read Websockify configuration from appsettings.json
            var websockifyOptions = new WebsockifyOptions();
            builder.Configuration.GetSection("Websockify").Bind(websockifyOptions);

            // Add the Websockify middleware to the application pipeline.
            app.UseWebsockify(websockifyOptions.Path, websockifyOptions.Host, websockifyOptions.Port);

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=VNC}/{action=Index}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
