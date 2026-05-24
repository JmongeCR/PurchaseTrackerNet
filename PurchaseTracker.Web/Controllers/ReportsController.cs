using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.Services;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("reports")]
public class ReportsController : Controller
{
    private readonly ReportService      _reports;
    private readonly PdfReportService   _pdf;
    private readonly ExcelReportService _excel;

    public ReportsController(
        ReportService reports,
        PdfReportService pdf,
        ExcelReportService excel)
    {
        _reports = reports;
        _pdf     = pdf;
        _excel   = excel;
    }

    // ── GET /reports ──────────────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);
        var (cards, categories) = await _reports.GetDropdownsAsync(userId);
        var banks = await _reports.GetBanksAsync(userId);

        var filter = new ReportFilterViewModel
        {
            Cards      = cards,
            Categories = categories,
            Month      = DateTime.Today.Month,
            Year       = DateTime.Today.Year,
        };

        ViewBag.Banks = banks;
        return View(filter);
    }

    // ── POST /reports/pdf ─────────────────────────────────────────────────
    [HttpPost("pdf")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportPdf(ReportFilterViewModel filter)
    {
        if (ClaimsHelper.IsAdmin(User)) return RedirectToAction("Index", "Admin");

        var userId   = ClaimsHelper.GetUserId(User);
        var userName = User.Identity?.Name ?? "Usuario";

        var (cards, categories) = await _reports.GetDropdownsAsync(userId);
        filter.Cards      = cards;
        filter.Categories = categories;

        var data = await _reports.BuildAsync(userId, userName, filter);
        var pdf  = _pdf.Generate(data);

        var filename = $"purchasetracker_{filter.ReportSource}_{DateTime.Today:yyyy-MM-dd}.pdf";
        return File(pdf, "application/pdf", filename);
    }

    // ── POST /reports/excel ───────────────────────────────────────────────
    [HttpPost("excel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportExcel(ReportFilterViewModel filter)
    {
        if (ClaimsHelper.IsAdmin(User)) return RedirectToAction("Index", "Admin");

        var userId   = ClaimsHelper.GetUserId(User);
        var userName = User.Identity?.Name ?? "Usuario";

        var (cards, categories) = await _reports.GetDropdownsAsync(userId);
        filter.Cards      = cards;
        filter.Categories = categories;

        var data = await _reports.BuildAsync(userId, userName, filter);
        var xlsx = _excel.Generate(data);

        var filename = $"purchasetracker_{filter.ReportSource}_{DateTime.Today:yyyy-MM-dd}.xlsx";
        return File(xlsx,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }
}
