using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
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

            var websockifySection = builder.Configuration.GetSection("Websockify");
            var websockifyOptions = new WebsockifyOptions
            {
                Path = websockifySection["Path"] ?? "/websockify",
                Host = websockifySection["Host"] ?? "127.0.0.1",
                Port = int.TryParse(websockifySection["Port"], out var port) ? port : 5900
            };

            builder.Services.AddAuthentication("BasicAuthentication")
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
            builder.Services.AddAuthorization();

            builder.Services.AddMemoryCache();

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

            app.UseWebsockify(websockifyOptions.Path, websockifyOptions.Host, websockifyOptions.Port);

            app.MapStaticAssets()
                .RequireAuthorization();

            MapVncEndpoints(app);

            app.Run();
        }

        private static void MapVncEndpoints(WebApplication app)
        {
            app.MapGet("/", (RequestDelegate)(context => ServeHtmlFile(context, "vnc.html")))
                .RequireAuthorization();

            app.MapGet("/Lite", (RequestDelegate)(context => ServeHtmlFile(context, "vnc_lite.html")))
                .RequireAuthorization();
        }

        private static async Task ServeHtmlFile(HttpContext context, string fileName)
        {
            var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

            try
            {
                var cacheKey = $"HtmlFile_{fileName}_CacheKey";

                if (cache.TryGetValue(cacheKey, out string? cachedContent) && cachedContent != null)
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(cachedContent);
                    return;
                }

                var filePath = Path.Combine(env.WebRootPath, fileName);
                if (!File.Exists(filePath))
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("页面走丢了呢");
                    return;
                }

                var fileContent = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("页面走丢了呢");
                    return;
                }

                fileContent = Regex.Replace(fileContent, "<html lang=\"[^\"]*\"", "<html lang=\"zh-CHS\"");

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(120),
                    SlidingExpiration = TimeSpan.FromMinutes(120)
                };

                cache.Set(cacheKey, fileContent, cacheOptions);

                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(fileContent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading {FileName}", fileName);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("页面走丢了呢");
            }
        }
    }
}
