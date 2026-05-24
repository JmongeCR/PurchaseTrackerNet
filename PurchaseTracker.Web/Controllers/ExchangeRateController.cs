using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurchaseTracker.Web.Services;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("exchange")]
public class ExchangeRateController : Controller
{
    private readonly ExchangeRateService _svc;
    public ExchangeRateController(ExchangeRateService svc) => _svc = svc;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var data = await _svc.GetRatesAsync();
        return View(data);
    }
}
