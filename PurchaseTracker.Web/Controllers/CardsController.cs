using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("cards")]
public class CardsController : Controller
{
    private readonly AppDbContext _db;
    public CardsController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);
        var now = DateTime.UtcNow;
        var monthStart  = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var threeMonthsAgo = now.AddMonths(-3);

        var cards = await _db.Cards
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Bank)
            .ToListAsync();

        // Load 3 months of transactions to cover both closed and open credit cycles
        var recentTx = await _db.Transactions
            .Where(t => t.UserId == userId && t.TransactionDate >= threeMonthsAgo && t.Status == "Aprobada")
            .ToListAsync();

        var cardStats = cards.Select(card =>
        {
            var monthTx = recentTx
                .Where(t => t.CardId == card.CardId && t.TransactionDate >= monthStart)
                .ToList();

            DateTime? lastCutDate    = null;
            DateTime? prevCutDate    = null;
            DateTime? nextCutDate    = null;
            DateTime? paymentDueDate = null;
            int daysUntilCut         = 0;
            decimal lastCycleSpending = 0;
            int lastCycleTxCount      = 0;
            decimal openCycleSpending = 0;
            int openCycleTxCount      = 0;

            if (card.CutDay.HasValue)
            {
                var cutDay = card.CutDay.Value;
                var today  = now.Date;

                // Cut date for the current calendar month (clamped to last day if needed)
                var cutThisMonth = new DateTime(today.Year, today.Month,
                    Math.Min(cutDay, DateTime.DaysInMonth(today.Year, today.Month)));

                if (today >= cutThisMonth)
                {
                    // Cut already happened this month
                    lastCutDate = cutThisMonth;
                    nextCutDate = cutThisMonth.AddMonths(1);
                }
                else
                {
                    // Cut hasn't happened yet — last cut was previous month
                    var prevCutMonthDate = cutThisMonth.AddMonths(-1);
                    lastCutDate = new DateTime(prevCutMonthDate.Year, prevCutMonthDate.Month,
                        Math.Min(cutDay, DateTime.DaysInMonth(prevCutMonthDate.Year, prevCutMonthDate.Month)));
                    nextCutDate = cutThisMonth;
                }

                // The cut before lastCutDate
                var prevMonth = lastCutDate.Value.AddMonths(-1);
                prevCutDate = new DateTime(prevMonth.Year, prevMonth.Month,
                    Math.Min(cutDay, DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month)));

                daysUntilCut = (int)(nextCutDate.Value.Date - today).TotalDays;

                if (card.PaymentDays.HasValue)
                    paymentDueDate = lastCutDate.Value.AddDays(card.PaymentDays.Value);

                // Closed cycle: day after prevCutDate → lastCutDate inclusive (already billed, to pay)
                var closedTx = recentTx
                    .Where(t => t.CardId == card.CardId && t.Currency == "CRC"
                             && t.TransactionDate.Date > prevCutDate.Value.Date
                             && t.TransactionDate.Date <= lastCutDate.Value.Date)
                    .ToList();
                lastCycleSpending = closedTx.Sum(t => t.Amount);
                lastCycleTxCount  = closedTx.Count;

                // Open cycle: day after lastCutDate → today (still accumulating)
                var openTx = recentTx
                    .Where(t => t.CardId == card.CardId && t.Currency == "CRC"
                             && t.TransactionDate.Date > lastCutDate.Value.Date)
                    .ToList();
                openCycleSpending = openTx.Sum(t => t.Amount);
                openCycleTxCount  = openTx.Count;
            }

            return new CardWithStats
            {
                Card                  = card,
                MonthSpendingCrc      = monthTx.Where(t => t.Currency == "CRC").Sum(t => t.Amount),
                MonthSpendingUsd      = monthTx.Where(t => t.Currency == "USD").Sum(t => t.Amount),
                MonthTransactionCount = monthTx.Count,
                LastTransactionDate   = monthTx.Count > 0 ? monthTx.Max(t => (DateTime?)t.TransactionDate) : null,
                LastCutDate           = lastCutDate,
                PrevCutDate           = prevCutDate,
                NextCutDate           = nextCutDate,
                PaymentDueDate        = paymentDueDate,
                DaysUntilCut          = daysUntilCut,
                LastCycleSpending     = lastCycleSpending,
                LastCycleTxCount      = lastCycleTxCount,
                OpenCycleSpending     = openCycleSpending,
                OpenCycleTxCount      = openCycleTxCount
            };
        }).ToList();

        var creditWithCycle = cardStats
            .Where(c => c.Card.CardType == "credito" && c.Card.IsActive && c.NextCutDate.HasValue)
            .ToList();

        return View(new CardListViewModel
        {
            Cards                    = cardStats,
            TotalToPayCrc            = creditWithCycle.Sum(c => c.LastCycleSpending),
            TotalOpenCycleCrc        = creditWithCycle.Sum(c => c.OpenCycleSpending),
            ActiveCreditCardsWithCycle = creditWithCycle.Count
        });
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddCardViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Error en el formulario.";
            return RedirectToAction(nameof(Index));
        }
        var userId = ClaimsHelper.GetUserId(User);
        var isCredit = model.CardType == "credito";
        _db.Cards.Add(new Card
        {
            UserId      = userId,
            Bank        = model.Bank,
            CardAlias   = model.CardAlias,
            Last4       = model.Last4,
            CardType    = model.CardType,
            CardBrand   = string.IsNullOrWhiteSpace(model.CardBrand) ? null : model.CardBrand,
            Currency    = model.Currency,
            CutDay      = isCredit ? model.CutDay      : null,
            PaymentDays = isCredit ? model.PaymentDays : null,
            CreditLimit = isCredit ? model.CreditLimit : null
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tarjeta agregada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditCardViewModel model)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.CardId == id && c.UserId == userId);
        if (card == null) return NotFound();

        var isCredit = model.CardType == "credito";
        card.Bank        = model.Bank;
        card.CardAlias   = model.CardAlias;
        card.Last4       = model.Last4;
        card.CardType    = model.CardType;
        card.CardBrand   = string.IsNullOrWhiteSpace(model.CardBrand) ? null : model.CardBrand;
        card.Currency    = model.Currency;
        card.CutDay      = isCredit ? model.CutDay      : null;
        card.PaymentDays = isCredit ? model.PaymentDays : null;
        card.CreditLimit = isCredit ? model.CreditLimit : null;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Tarjeta actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.CardId == id && c.UserId == userId);
        if (card != null)
        {
            card.IsActive = !card.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.CardId == id && c.UserId == userId);
        if (card != null) { _db.Cards.Remove(card); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Tarjeta eliminada.";
        return RedirectToAction(nameof(Index));
    }
}
