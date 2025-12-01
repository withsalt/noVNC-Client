using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using noVNCClient.Models;

namespace noVNCClient.Controllers
{
    public class HomeController : Controller
    {
        [Route("")]
        [Route("Index")]
        public IActionResult Index()
        {
            return View();
        }

        [Route("Lite")]
        public IActionResult Lite()
        {
            return View();
        }
    }
}
