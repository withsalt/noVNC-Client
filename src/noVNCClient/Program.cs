using Microsoft.AspNetCore.Authentication;
using noVNCClient.Authentication;
using noVNCClient.Middlewares;
using noVNCClient.Models;

namespace noVNCClient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Read Websockify configuration from appsettings.json
            var websockifyOptions = new WebsockifyOptions();
            builder.Configuration.GetSection("Websockify").Bind(websockifyOptions);

            builder.Services.AddAuthentication("BasicAuthentication")
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
            builder.Services.AddAuthorization();

            builder.Services.AddMemoryCache();

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // Enforce authentication for Websockify path
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments(websockifyOptions.Path))
                {
                    if (!context.User.Identity?.IsAuthenticated ?? true)
                    {
                        await context.ChallengeAsync();
                        return;
                    }
                }
                await next();
            });

            // Add the Websockify middleware to the application pipeline.
            app.UseWebsockify(websockifyOptions.Path, websockifyOptions.Host, websockifyOptions.Port);

            app.MapStaticAssets()
                .RequireAuthorization();
            app.MapControllerRoute(name: "default", pattern: "{controller=VNC}/{action=Index}").WithStaticAssets()
                .RequireAuthorization();

            app.Run();
        }
    }
}
