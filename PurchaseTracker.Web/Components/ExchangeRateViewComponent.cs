using Microsoft.AspNetCore.Mvc;
using PurchaseTracker.Web.Services;

namespace PurchaseTracker.Web.Components;

/// <summary>
/// ViewComponent que muestra el tipo de cambio USD/CRC en el topbar.
/// Se invoca con @await Component.InvokeAsync("ExchangeRate").
/// Nunca rompe la carga de la página — si falla la API, muestra un estado neutro.
/// </summary>
public class ExchangeRateViewComponent : ViewComponent
{
    private readonly ExchangeRateService _svc;
    public ExchangeRateViewComponent(ExchangeRateService svc) => _svc = svc;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var data = await _svc.GetRatesAsync();
        return View(data);
    }
}
