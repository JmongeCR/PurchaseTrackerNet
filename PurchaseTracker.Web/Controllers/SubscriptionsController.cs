using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("subscriptions")]
public class SubscriptionsController : Controller
{
    private readonly AppDbContext _db;
    public SubscriptionsController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(string tab = "activas")
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);

        // Auto-advance overdue active subscriptions
        var today = DateTime.UtcNow.Date;
        var overdue = await _db.Subscriptions
            .Where(s => s.UserId == userId && s.Status == "activa" && s.NextBillingDate < today)
            .ToListAsync();

        if (overdue.Count > 0)
        {
            foreach (var sub in overdue)
            {
                while (sub.NextBillingDate.Date < today)
                    sub.NextBillingDate = AdvanceDate(sub.NextBillingDate, sub.Frequency);
                sub.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            TempData["Info"] = overdue.Count == 1
                ? $"Se actualizó la fecha de renovación de \"{overdue[0].Name}\"."
                : $"Se actualizaron las fechas de renovación de {overdue.Count} suscripciones.";
        }

        var all = await _db.Subscriptions
            .Include(s => s.Card)
            .Include(s => s.Category)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.NextBillingDate)
            .ToListAsync();

        var cards = await _db.Cards
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Bank)
            .ToListAsync();

        var categories = await _db.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var activas = all.Where(s => s.Status == "activa").ToList();

        var vm = new SubscriptionListViewModel
        {
            Activas    = activas,
            Pausadas   = all.Where(s => s.Status == "pausada").ToList(),
            Canceladas = all.Where(s => s.Status == "cancelada").ToList(),
            ActiveCount = activas.Count,
            TotalMonthlyCrc = activas.Where(s => s.Currency == "CRC").Sum(s => s.MonthlyEquivalent),
            TotalMonthlyUsd = activas.Where(s => s.Currency == "USD").Sum(s => s.MonthlyEquivalent),
            TotalAnnualCrc  = activas.Where(s => s.Currency == "CRC").Sum(s => s.MonthlyEquivalent) * 12,
            TotalAnnualUsd  = activas.Where(s => s.Currency == "USD").Sum(s => s.MonthlyEquivalent) * 12,
            Cards      = cards,
            Categories = categories,
            Tab        = tab
        };

        return View(vm);
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddSubscriptionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.Amount <= 0)
        {
            TempData["Error"] = "El nombre y el monto son obligatorios.";
            return RedirectToAction(nameof(Index));
        }

        var userId = ClaimsHelper.GetUserId(User);

        _db.Subscriptions.Add(new Subscription
        {
            UserId          = userId,
            CardId          = model.CardId,
            CategoryId      = model.CategoryId,
            Name            = model.Name,
            Merchant        = model.Merchant,
            Bank            = model.Bank,
            Amount          = model.Amount,
            Currency        = model.Currency,
            Frequency       = model.Frequency,
            StartDate       = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc),
            NextBillingDate = DateTime.SpecifyKind(model.NextBillingDate, DateTimeKind.Utc),
            Notes           = model.Notes
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Suscripción \"{model.Name}\" registrada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSubscriptionViewModel model)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var s = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id && x.UserId == userId);
        if (s == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name) || model.Amount <= 0)
        {
            TempData["Error"] = "El nombre y el monto son obligatorios.";
            return RedirectToAction(nameof(Index));
        }

        s.Name            = model.Name;
        s.Merchant        = model.Merchant;
        s.Bank            = model.Bank;
        s.CardId          = model.CardId;
        s.CategoryId      = model.CategoryId;
        s.Amount          = model.Amount;
        s.Currency        = model.Currency;
        s.Frequency       = model.Frequency;
        s.StartDate       = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
        s.NextBillingDate = DateTime.SpecifyKind(model.NextBillingDate, DateTimeKind.Utc);
        s.Notes           = model.Notes;
        s.UpdatedAt       = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Suscripción \"{s.Name}\" actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var s = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id && x.UserId == userId);
        if (s == null) return NotFound();

        if (s.Status == "activa")
        {
            s.Status = "pausada";
            TempData["Success"] = $"Suscripción \"{s.Name}\" pausada.";
        }
        else if (s.Status == "pausada")
        {
            s.Status = "activa";
            TempData["Success"] = $"Suscripción \"{s.Name}\" reactivada.";
        }

        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cancel/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var s = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id && x.UserId == userId);
        if (s == null) return NotFound();

        s.Status    = "cancelada";
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Suscripción \"{s.Name}\" cancelada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var s = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id && x.UserId == userId);
        if (s == null) return NotFound();

        _db.Subscriptions.Remove(s);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Suscripción eliminada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("renew/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var s = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id && x.UserId == userId);
        if (s == null) return NotFound();

        s.NextBillingDate = AdvanceDate(s.NextBillingDate, s.Frequency);
        s.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Renovación de \"{s.Name}\" registrada. Próxima: {s.NextBillingDate:dd/MM/yyyy}.";
        return RedirectToAction(nameof(Index));
    }

    private static DateTime AdvanceDate(DateTime date, string frequency) => frequency switch
    {
        "anual"      => date.AddMonths(12),
        "trimestral" => date.AddMonths(3),
        "semanal"    => date.AddDays(7),
        _            => date.AddMonths(1)   // mensual
    };
}
