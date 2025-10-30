using Microsoft.AspNetCore.Mvc;

namespace Application.Areas.Home.Controllers;

[Area("Home")]
public class ErrorController : Controller
{
    [Route("/error/{statusCode}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index(int statusCode)
    {
        if (HttpContext.Response.HasStarted)
        {
            return new EmptyResult();
        }

        ViewData["RequestId"] = HttpContext.TraceIdentifier;

        if (statusCode == 403)
            return View("Forbidden");
        return View("Error");
    }
}
