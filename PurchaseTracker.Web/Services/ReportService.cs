using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Services;

public class ReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;

    // ── Punto de entrada principal ────────────────────────────────────────
    public async Task<ReportData> BuildAsync(
        int userId, string userName, ReportFilterViewModel filter)
    {
        var rows = filter.ReportSource switch
        {
            "financiamientos" => await QueryFinancingsAsync(userId, filter),
            "notas"           => await QueryNotesAsync(userId, filter),
            _                 => await QueryTransactionsAsync(userId, filter)
        };

        // Orden cronológico
        rows = [.. rows.OrderBy(r => r.Date).ThenBy(r => r.Merchant)];

        return new ReportData
        {
            Filter      = filter,
            Rows        = rows,
            TotalCrc    = rows.Where(r => r.Currency == "CRC").Sum(r => r.Amount),
            TotalUsd    = rows.Where(r => r.Currency == "USD").Sum(r => r.Amount),
            GeneratedAt = DateTime.Now,
            UserName    = userName
        };
    }

    // ── Movimientos ───────────────────────────────────────────────────────
    private async Task<List<ReportTransactionRow>> QueryTransactionsAsync(
        int userId, ReportFilterViewModel f)
    {
        var q = _db.Transactions
            .Include(t => t.Card)
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .AsQueryable();

        q = ApplyDateFilter(q, f);

        if (!string.IsNullOrWhiteSpace(f.Bank))         q = q.Where(t => t.Bank == f.Bank);
        if (!string.IsNullOrWhiteSpace(f.Currency))     q = q.Where(t => t.Currency == f.Currency);
        if (f.CategoryId.HasValue)                       q = q.Where(t => t.CategoryId == f.CategoryId);
        if (f.CardId.HasValue)                           q = q.Where(t => t.CardId == f.CardId);
        if (!string.IsNullOrWhiteSpace(f.MovementType)) q = q.Where(t => t.MovementType == f.MovementType);
        if (!string.IsNullOrWhiteSpace(f.CardType))     q = q.Where(t => t.Card != null && t.Card.CardType == f.CardType);
        if (!string.IsNullOrWhiteSpace(f.Status))       q = q.Where(t => t.Status == f.Status);

        return (await q.ToListAsync()).Select(t => new ReportTransactionRow
        {
            Source       = "transaccion",
            SourceId     = t.TransactionId,
            Date         = t.TransactionDate,
            Bank         = t.Bank,
            Card         = t.Card?.DisplayName ?? "",
            Merchant     = t.Merchant,
            Category     = t.Category?.Name ?? "",
            MovementType = t.MovementType,
            Currency     = t.Currency,
            Amount       = t.Amount,
            Status       = t.Status
        }).ToList();
    }

    // ── Financiamientos ───────────────────────────────────────────────────
    private async Task<List<ReportTransactionRow>> QueryFinancingsAsync(
        int userId, ReportFilterViewModel f)
    {
        var q = _db.Financings
            .Include(fn => fn.Card)
            .Where(fn => fn.UserId == userId)
            .AsQueryable();

        // Fecha → PurchaseDate o periodo activo solapado
        if (f.Month.HasValue && f.Year.HasValue)
        {
            var start = new DateTime(f.Year.Value, f.Month.Value, 1);
            var end   = start.AddMonths(1);
            q = q.Where(fn => fn.PurchaseDate < end &&
                              fn.StartDate.AddMonths(fn.Months) >= start);
        }
        else
        {
            if (f.StartDate.HasValue)
                q = q.Where(fn => fn.PurchaseDate >= f.StartDate.Value.Date);
            if (f.EndDate.HasValue)
                q = q.Where(fn => fn.PurchaseDate <= f.EndDate.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(f.Bank))     q = q.Where(fn => fn.Bank == f.Bank);
        if (!string.IsNullOrWhiteSpace(f.Currency)) q = q.Where(fn => fn.Currency == f.Currency);
        if (f.CardId.HasValue)                       q = q.Where(fn => fn.CardId == f.CardId);
        if (!string.IsNullOrWhiteSpace(f.Status))   q = q.Where(fn => fn.Status == f.Status);

        return (await q.ToListAsync()).Select(fn =>
        {
            var typeLabel = fn.FinancingType switch
            {
                "cuotas_normales"  => "Cuotas normales",
                "cuotas_sin_inter" => "Sin intereses",
                "cuotas_con_inter" => "Con intereses",
                "cuotas_diferidas" => "Diferido",
                _                  => fn.FinancingType
            };
            return new ReportTransactionRow
            {
                Source       = "financiamiento",
                SourceId     = fn.FinancingId,
                Date         = fn.PurchaseDate,
                Bank         = fn.Bank,
                Card         = fn.Card?.DisplayName ?? "",
                Merchant     = fn.Merchant,
                Category     = "Financiamiento",
                MovementType = typeLabel,
                Currency     = fn.Currency,
                Amount       = fn.MonthlyPayment,
                Status       = fn.Status,
                Detail       = $"Cuota {fn.PaidInstallments}/{fn.Months} · " +
                               $"Pendiente: {fn.RemainingInstallments} cuota(s) · " +
                               $"Total: {FormatAmount(fn.TotalAmount, fn.Currency)}"
            };
        }).ToList();
    }

    // ── Notas / Compartidos ───────────────────────────────────────────────
    private async Task<List<ReportTransactionRow>> QueryNotesAsync(
        int userId, ReportFilterViewModel f)
    {
        var q = _db.PersonalNotes
            .Include(n => n.Category)
            .Where(n => n.UserId == userId)
            .AsQueryable();

        // Fecha
        if (f.Month.HasValue && f.Year.HasValue)
        {
            var s = new DateTime(f.Year.Value, f.Month.Value, 1);
            q = q.Where(n => n.NoteDate >= s && n.NoteDate < s.AddMonths(1));
        }
        else
        {
            if (f.StartDate.HasValue) q = q.Where(n => n.NoteDate >= f.StartDate.Value.Date);
            if (f.EndDate.HasValue)   q = q.Where(n => n.NoteDate < f.EndDate.Value.Date.AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(f.Currency))  q = q.Where(n => n.Currency == f.Currency);
        if (!string.IsNullOrWhiteSpace(f.Person))    q = q.Where(n => n.Person != null &&
                                                                       n.Person.Contains(f.Person));
        if (!string.IsNullOrWhiteSpace(f.Direction)) q = q.Where(n => n.Direction == f.Direction);
        if (!string.IsNullOrWhiteSpace(f.Status))    q = q.Where(n => n.Status == f.Status);
        if (f.CategoryId.HasValue)                   q = q.Where(n => n.CategoryId == f.CategoryId);

        return (await q.ToListAsync()).Select(n =>
        {
            var dirLabel = n.Direction == "cobrar" ? "Por cobrar" : "Por pagar";
            return new ReportTransactionRow
            {
                Source        = "nota",
                SourceId      = n.NoteId,
                Date          = n.NoteDate,
                Bank          = "",
                Card          = "",
                Merchant      = n.Title,
                Category      = n.Category?.Name ?? "",
                MovementType  = dirLabel,
                Currency      = n.Currency,
                Amount        = n.Amount,
                Status        = n.Status,
                Person        = n.Person ?? "",
                PaidAmount    = n.PaidAmount,
                PendingAmount = n.PendingAmount,
                Detail        = string.IsNullOrWhiteSpace(n.Description)
                                ? n.Notes
                                : n.Description
            };
        }).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static IQueryable<Transaction> ApplyDateFilter(
        IQueryable<Transaction> q, ReportFilterViewModel f)
    {
        if (f.Month.HasValue && f.Year.HasValue)
        {
            var s = new DateTime(f.Year.Value, f.Month.Value, 1);
            return q.Where(t => t.TransactionDate >= s && t.TransactionDate < s.AddMonths(1));
        }
        if (f.StartDate.HasValue) q = q.Where(t => t.TransactionDate >= f.StartDate.Value.Date);
        if (f.EndDate.HasValue)   q = q.Where(t => t.TransactionDate < f.EndDate.Value.Date.AddDays(1));
        return q;
    }

    private static string FormatAmount(decimal v, string cur) =>
        cur == "USD" ? $"${v:N2}" : $"₡{v:N2}";

    // ── Dropdowns para el formulario ──────────────────────────────────────
    public async Task<(List<Card> cards, List<Category> categories)> GetDropdownsAsync(int userId)
    {
        var cards = await _db.Cards
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Bank).ThenBy(c => c.CardAlias)
            .ToListAsync();

        var cats = await _db.Categories
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return (cards, cats);
    }

    public async Task<List<string>> GetBanksAsync(int userId)
    {
        var txBanks = await _db.Transactions
            .Where(t => t.UserId == userId && t.Bank != "")
            .Select(t => t.Bank).Distinct().ToListAsync();

        var fnBanks = await _db.Financings
            .Where(fn => fn.UserId == userId && fn.Bank != "")
            .Select(fn => fn.Bank).Distinct().ToListAsync();

        return txBanks.Union(fnBanks).OrderBy(b => b).ToList();
    }
}
