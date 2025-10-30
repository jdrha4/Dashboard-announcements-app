using Microsoft.AspNetCore.Mvc;

namespace Application.Areas.Home.Controllers;

[Area("Home")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "UserDashboards", new { area = "Dashboards" });

        return View();
    }
}
