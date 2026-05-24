using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("financing")]
public class FinancingController : Controller
{
    private readonly AppDbContext _db;
    public FinancingController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(string tab = "activos")
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);

        var financings = await _db.Financings
            .Include(f => f.Card)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        var cards = await _db.Cards
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Bank)
            .ToListAsync();

        var activos = financings.Where(f => f.Status == "activo").ToList();

        var vm = new FinancingListViewModel
        {
            Activos = activos,
            Pagados = financings.Where(f => f.Status == "pagado").ToList(),
            Cancelados = financings.Where(f => f.Status == "cancelado").ToList(),
            ActiveCount = activos.Count,
            TotalMonthlyCommitment    = activos.Where(f => f.Currency == "CRC").Sum(f => f.MonthlyPayment),
            TotalPendingAmount        = activos.Where(f => f.Currency == "CRC").Sum(f => f.PendingAmount),
            TotalMonthlyCommitmentUsd = activos.Where(f => f.Currency == "USD").Sum(f => f.MonthlyPayment),
            TotalPendingAmountUsd     = activos.Where(f => f.Currency == "USD").Sum(f => f.PendingAmount),
            Cards = cards,
            Tab = tab
        };

        return View(vm);
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddFinancingViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Merchant) || model.TotalAmount <= 0 || model.Months <= 0)
        {
            TempData["Error"] = "Completa todos los campos requeridos.";
            return RedirectToAction(nameof(Index));
        }

        var userId = ClaimsHelper.GetUserId(User);

        _db.Financings.Add(new Financing
        {
            UserId = userId,
            CardId = model.CardId,
            Merchant = model.Merchant,
            Bank = model.Bank,
            TotalAmount = model.TotalAmount,
            Currency = model.Currency,
            PurchaseDate = DateTime.SpecifyKind(model.PurchaseDate, DateTimeKind.Utc),
            FinancingType = model.FinancingType,
            Months = model.Months,
            MonthlyPayment = model.MonthlyPayment,
            InterestRate = model.InterestRate,
            Commission = model.Commission,
            StartDate = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc),
            Notes = model.Notes
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Financiamiento \"{model.Merchant}\" registrado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditFinancingViewModel model)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var f = await _db.Financings.FirstOrDefaultAsync(x => x.FinancingId == id && x.UserId == userId);
        if (f == null) return NotFound();

        // Validate monthly payment and paid installments
        if (model.MonthlyPayment <= 0)
        {
            TempData["Error"] = "La cuota mensual debe ser mayor a 0.";
            return RedirectToAction(nameof(Index));
        }
        if (model.PaidInstallments < 0 || model.PaidInstallments > model.Months)
        {
            TempData["Error"] = $"Las cuotas pagadas ({model.PaidInstallments}) no pueden ser negativas ni superar el total de meses ({model.Months}).";
            return RedirectToAction(nameof(Index));
        }

        f.CardId         = model.CardId;
        f.Merchant       = model.Merchant;
        f.Bank           = model.Bank;
        f.TotalAmount    = model.TotalAmount;
        f.Currency       = model.Currency;
        f.PurchaseDate   = DateTime.SpecifyKind(model.PurchaseDate, DateTimeKind.Utc);
        f.FinancingType  = model.FinancingType;
        f.Months         = model.Months;
        f.MonthlyPayment = model.MonthlyPayment;
        f.InterestRate   = model.InterestRate;
        f.Commission     = model.Commission;
        f.StartDate      = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
        f.Notes          = model.Notes;
        f.PaidInstallments = model.PaidInstallments;
        f.UpdatedAt      = DateTime.UtcNow;

        // Auto-mark as paid if all installments covered
        if (f.PaidInstallments >= f.Months)
        {
            f.Status = "pagado";
            var pending = Math.Round(f.PendingAmount, 2);
            TempData["Success"] = $"✅ Financiamiento \"{f.Merchant}\" marcado como pagado (cuota: {(f.Currency == "USD" ? "$" : "₡")} {f.MonthlyPayment:N2}).";
        }
        else
        {
            var remaining = f.RemainingInstallments;
            var pending   = Math.Round(f.PendingAmount, 2);
            TempData["Success"] = $"Financiamiento actualizado. Cuota: {(f.Currency == "USD" ? "$" : "₡")} {f.MonthlyPayment:N2} · Pendiente: {(f.Currency == "USD" ? "$" : "₡")} {pending:N2} ({remaining} cuota(s)).";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("pay/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayInstallment(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var f = await _db.Financings.FirstOrDefaultAsync(x => x.FinancingId == id && x.UserId == userId);
        if (f == null) return NotFound();

        if (f.Status != "activo")
        {
            TempData["Error"] = "Este financiamiento no está activo.";
            return RedirectToAction(nameof(Index));
        }

        f.PaidInstallments++;

        if (f.PaidInstallments >= f.Months)
        {
            f.Status = "pagado";
            TempData["Success"] = $"✅ ¡Financiamiento \"{f.Merchant}\" completamente pagado!";
        }
        else
        {
            TempData["Success"] = $"Cuota registrada. Quedan {f.RemainingInstallments} cuota(s) por pagar.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("reduce/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReduceInstallment(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var f = await _db.Financings.FirstOrDefaultAsync(x => x.FinancingId == id && x.UserId == userId);
        if (f == null) return NotFound();

        if (f.PaidInstallments <= 0)
        {
            TempData["Error"] = "No hay cuotas pagadas que reducir.";
            return RedirectToAction(nameof(Index));
        }

        f.PaidInstallments--;
        f.UpdatedAt = DateTime.UtcNow;

        // If it was fully paid, reactivate it
        if (f.Status == "pagado")
        {
            f.Status = "activo";
            TempData["Success"] = $"Cuota reducida. \"{f.Merchant}\" reactivado — {f.PaidInstallments} pagadas, {f.RemainingInstallments} por pagar.";
        }
        else
        {
            TempData["Success"] = $"Cuota reducida. \"{f.Merchant}\" ahora tiene {f.PaidInstallments} pagadas, {f.RemainingInstallments} por pagar.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cancel/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var f = await _db.Financings.FirstOrDefaultAsync(x => x.FinancingId == id && x.UserId == userId);
        if (f == null) return NotFound();

        f.Status = "cancelado";
        await _db.SaveChangesAsync();
        TempData["Success"] = "Financiamiento cancelado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var f = await _db.Financings.FirstOrDefaultAsync(x => x.FinancingId == id && x.UserId == userId);
        if (f == null) return NotFound();

        _db.Financings.Remove(f);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Financiamiento eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
