using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PurchaseTracker.Web.Controllers;

[AllowAnonymous]
public class LandingController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View();
    }

    // Permite ver la landing page incluso estando autenticado
    [HttpGet("/home")]
    public IActionResult Home() => View("Index");
}
