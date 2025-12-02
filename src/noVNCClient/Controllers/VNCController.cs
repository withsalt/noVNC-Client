using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace noVNCClient.Controllers
{
    [Authorize]
    public class VNCController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VNCController> _logger;

        public VNCController(IWebHostEnvironment env, IMemoryCache cache, ILogger<VNCController> logger)
        {
            _env = env;
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Route("")]
        public IActionResult Index()
        {
            try
            {
                var fileContent = ReadHtmlFile("vnc.html");
                return Content(fileContent, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading vnc.html");
                return NotFound("页面走丢了呢");
            }
        }

        
        [Route("Lite")]
        public IActionResult Lite()
        {
            try
            {
                var fileContent = ReadHtmlFile("vnc_lite.html");
                return Content(fileContent, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading vnc_lite.html");
                return NotFound("页面走丢了呢");
            }
        }

        private string ReadHtmlFile(string fileName)
        {
            var cacheKey = $"HtmlFile_{fileName}_CacheKey";

            // 尝试从缓存中获取
            if (_cache.TryGetValue(cacheKey, out string? cachedContent) && cachedContent != null)
            {
                return cachedContent;
            }

            // 缓存未命中，从磁盘读取
            var filePath = Path.Combine(_env.WebRootPath, fileName);
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("HTML file not found", fileName);
            }

            var fileContent = System.IO.File.ReadAllText(filePath);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(120),
                SlidingExpiration = TimeSpan.FromMinutes(120)
            };

            _cache.Set(cacheKey, fileContent, cacheOptions);

            return fileContent;
        }
    }
}
