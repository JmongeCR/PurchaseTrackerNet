using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.Services;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
public class DashboardController : Controller
{
    private readonly AppDbContext        _db;
    private readonly ExchangeRateService _er;

    public DashboardController(AppDbContext db, ExchangeRateService er)
    {
        _db = db;
        _er = er;
    }

    public async Task<IActionResult> Index()
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = monthStart.AddMonths(-1);

        var txThisMonth = await _db.Transactions
            .Include(t => t.Category)
            .Include(t => t.Card)
            .Where(t => t.UserId == userId && t.TransactionDate >= monthStart && t.Status == "Aprobada")
            .ToListAsync();

        var txPrevMonth = await _db.Transactions
            .Where(t => t.UserId == userId && t.TransactionDate >= prevMonthStart && t.TransactionDate < monthStart && t.Status == "Aprobada")
            .ToListAsync();

        var recent = await _db.Transactions
            .Include(t => t.Category)
            .Include(t => t.Card)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(10)
            .ToListAsync();

        var dailyRaw = txThisMonth
            .Where(t => t.Currency == "CRC")
            .GroupBy(t => t.TransactionDate.Date)
            .Select(g => new { date = g.Key.ToString("dd/MM"), total = g.Sum(x => x.Amount) })
            .OrderBy(x => x.date)
            .ToList();

        var topMerchants = txThisMonth
            .Where(t => t.Currency == "CRC")
            .GroupBy(t => t.Merchant)
            .Select(g => new TopMerchant { Merchant = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToList();

        var spendingByCat = txThisMonth
            .Where(t => t.Currency == "CRC" && t.Category != null)
            .GroupBy(t => t.Category!)
            .Select(g => new SpendingByCategory
            {
                CategoryName = g.Key.Name,
                Color = g.Key.Color,
                Icon = g.Key.IconEmoji,
                Total = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Total)
            .Take(6)
            .ToList();

        var activeCards = await _db.Cards.CountAsync(c => c.UserId == userId && c.IsActive);

        // Phase 2: Financings
        var activeFinancings = await _db.Financings
            .Where(f => f.UserId == userId && f.Status == "activo")
            .ToListAsync();

        var upcomingPayments = activeFinancings
            .Where(f => f.NextPaymentDate.HasValue)
            .OrderBy(f => f.NextPaymentDate)
            .Take(5)
            .Select(f => new UpcomingPayment
            {
                Merchant = f.Merchant,
                NextDate = f.NextPaymentDate,
                Amount = f.MonthlyPayment,
                Currency = f.Currency,
                RemainingInstallments = f.RemainingInstallments
            })
            .ToList();

        // Phase 2: Personal Notes
        var activeNotes = await _db.PersonalNotes
            .Where(n => n.UserId == userId && n.Status != "pagado")
            .ToListAsync();

        // Tipo de cambio (paralelo — no bloquea el resto)
        var erTask = _er.GetRatesAsync();

        // Subscriptions
        var activeSubs = await _db.Subscriptions
            .Where(s => s.UserId == userId && s.Status == "activa")
            .ToListAsync();

        var upcomingSubs = activeSubs
            .OrderBy(s => s.NextBillingDate)
            .Take(5)
            .Select(s => new UpcomingSubscription
            {
                Name            = s.Name,
                NextBillingDate = s.NextBillingDate,
                Amount          = s.Amount,
                Currency        = s.Currency,
                Frequency       = s.Frequency,
                MonthlyEquivalent = s.MonthlyEquivalent
            })
            .ToList();

        // Combined upcoming payments (financing + subscriptions)
        var nextPayments = upcomingPayments
            .Where(p => p.NextDate.HasValue)
            .Select(p => new DashboardPaymentItem
            {
                Name     = p.Merchant,
                Date     = p.NextDate!.Value,
                Amount   = p.Amount,
                Currency = p.Currency,
                Type     = "financing",
                Detail   = $"{p.RemainingInstallments} cuota(s) restantes"
            })
            .Concat(upcomingSubs.Select(s => new DashboardPaymentItem
            {
                Name     = s.Name,
                Date     = s.NextBillingDate,
                Amount   = s.Amount,
                Currency = s.Currency,
                Type     = "subscription",
                Detail   = s.Frequency switch
                {
                    "anual"      => "Anual",
                    "trimestral" => "Trimestral",
                    "semanal"    => "Semanal",
                    _            => "Mensual"
                }
            }))
            .OrderBy(x => x.Date)
            .Take(8)
            .ToList();

        var vm = new DashboardViewModel
        {
            MonthSpendingCrc = txThisMonth.Where(t => t.Currency == "CRC").Sum(t => t.Amount),
            MonthSpendingUsd = txThisMonth.Where(t => t.Currency == "USD").Sum(t => t.Amount),
            MonthUsdTransactionCount = txThisMonth.Count(t => t.Currency == "USD"),
            MonthTransactionCount = txThisMonth.Count,
            PrevMonthSpendingCrc = txPrevMonth.Where(t => t.Currency == "CRC").Sum(t => t.Amount),
            ActiveCardsCount = activeCards,
            RecentTransactions = recent,
            TopMerchants = topMerchants,
            SpendingByCategory = spendingByCat,
            DailySpendingJson = JsonSerializer.Serialize(dailyRaw),
            CurrentMonthLabel = now.ToString("MMMM yyyy"),

            // Phase 2
            ActiveFinancingsCount = activeFinancings.Count,
            TotalMonthlyCommitment    = activeFinancings.Where(f => f.Currency == "CRC").Sum(f => f.MonthlyPayment),
            TotalPendingFinancing     = activeFinancings.Where(f => f.Currency == "CRC").Sum(f => f.PendingAmount),
            TotalMonthlyCommitmentUsd = activeFinancings.Where(f => f.Currency == "USD").Sum(f => f.MonthlyPayment),
            TotalPendingFinancingUsd  = activeFinancings.Where(f => f.Currency == "USD").Sum(f => f.PendingAmount),
            TotalToCollect = activeNotes.Where(n => n.Direction == "cobrar").Sum(n => n.PendingAmount),
            TotalToPay = activeNotes.Where(n => n.Direction == "pagar").Sum(n => n.PendingAmount),
            UpcomingPayments = upcomingPayments,

            // Subscriptions
            ActiveSubscriptionsCount  = activeSubs.Count,
            TotalMonthlySubscriptionsCrc = activeSubs.Where(s => s.Currency == "CRC").Sum(s => s.MonthlyEquivalent),
            TotalMonthlySubscriptionsUsd = activeSubs.Where(s => s.Currency == "USD").Sum(s => s.MonthlyEquivalent),
            TotalAnnualEstimatedCrc   = activeSubs.Where(s => s.Currency == "CRC").Sum(s => s.MonthlyEquivalent) * 12,
            UpcomingSubscriptions     = upcomingSubs,
            NextPayments              = nextPayments,
            ExchangeRate              = await erTask
        };

        return View(vm);
    }
}
