using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("categories")]
public class CategoriesController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Settings");
    }
}
