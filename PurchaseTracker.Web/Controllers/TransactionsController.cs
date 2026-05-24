using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("transactions")]
public class TransactionsController : Controller
{
    private readonly AppDbContext _db;
    public TransactionsController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, string? bank, string? currency, string? movementType,
        string? status, int? categoryId, int? cardId,
        DateTime? startDate, DateTime? endDate,
        string sort = "date_desc", int page = 1)
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);

        var query = _db.Transactions
            .Include(t => t.Card)
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Merchant.Contains(search) || (t.Description != null && t.Description.Contains(search)));
        if (!string.IsNullOrWhiteSpace(bank))
            query = query.Where(t => t.Bank == bank);
        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(t => t.Currency == currency);
        if (!string.IsNullOrWhiteSpace(movementType))
            query = query.Where(t => t.MovementType == movementType);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);
        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId);
        if (cardId.HasValue)
            query = query.Where(t => t.CardId == cardId);
        if (startDate.HasValue)
            query = query.Where(t => t.TransactionDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(t => t.TransactionDate <= endDate.Value.AddDays(1));

        query = sort switch
        {
            "date_asc" => query.OrderBy(t => t.TransactionDate),
            "amount_desc" => query.OrderByDescending(t => t.Amount),
            "amount_asc" => query.OrderBy(t => t.Amount),
            _ => query.OrderByDescending(t => t.TransactionDate)
        };

        var total = await query.CountAsync();
        var totalCrc = await query.Where(t => t.Currency == "CRC").SumAsync(t => (decimal?)t.Amount) ?? 0;
        var totalUsd = await query.Where(t => t.Currency == "USD").SumAsync(t => (decimal?)t.Amount) ?? 0;

        const int pageSize = 20;
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var cards = await _db.Cards.Where(c => c.UserId == userId && c.IsActive).ToListAsync();
        var categories = await _db.Categories.Where(c => c.UserId == userId && c.IsActive).ToListAsync();

        var vm = new TransactionListViewModel
        {
            Transactions = items, Cards = cards, Categories = categories,
            Search = search, Bank = bank, Currency = currency, MovementType = movementType,
            Status = status, CategoryId = categoryId, CardId = cardId,
            StartDate = startDate, EndDate = endDate, Sort = sort,
            Page = page, PageSize = pageSize, TotalCount = total,
            TotalCrc = totalCrc, TotalUsd = totalUsd
        };
        return View(vm);
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddTransactionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Error en el formulario. Verifica los campos.";
            return RedirectToAction(nameof(Index));
        }
        var userId = ClaimsHelper.GetUserId(User);
        var tx = new Transaction
        {
            UserId = userId,
            Bank = model.Bank,
            Merchant = model.Merchant,
            Amount = model.Amount,
            Currency = model.Currency,
            TransactionDate = model.TransactionDate,
            Status = model.Status,
            Description = model.Description,
            Notes = model.Notes,
            CardId = model.CardId,
            CategoryId = model.CategoryId,
            MovementType = model.MovementType,
            Origin = "manual"
        };
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Transacción registrada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditTransactionViewModel model)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);
        if (tx == null) return NotFound();

        tx.Bank            = model.Bank;
        tx.Merchant        = model.Merchant;
        tx.Amount          = model.Amount;
        tx.Currency        = model.Currency;
        tx.TransactionDate = model.TransactionDate;
        tx.Status          = model.Status;
        tx.Description     = model.Description;
        tx.Notes           = model.Notes;
        tx.CardId          = model.CardId;
        tx.CategoryId      = model.CategoryId;
        tx.MovementType    = model.MovementType;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Transacción actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("duplicate/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var src = await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);
        if (src == null) return NotFound();

        _db.Transactions.Add(new Transaction
        {
            UserId          = userId,
            Bank            = src.Bank,
            Merchant        = src.Merchant,
            Amount          = src.Amount,
            Currency        = src.Currency,
            TransactionDate = DateTime.UtcNow.Date,
            Status          = src.Status,
            Description     = src.Description,
            Notes           = src.Notes,
            CardId          = src.CardId,
            CategoryId      = src.CategoryId,
            MovementType    = src.MovementType,
            Origin          = "manual"
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Transacción \"{src.Merchant}\" duplicada con fecha de hoy.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);
        if (tx != null)
        {
            _db.Transactions.Remove(tx);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Transacción eliminada.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("assign-category")]
    public async Task<IActionResult> AssignCategory(int transactionId, int? categoryId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);
        if (tx == null) return NotFound();
        tx.CategoryId = categoryId;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        string? search, string? bank, string? currency, string? movementType,
        string? status, int? categoryId, int? cardId,
        DateTime? startDate, DateTime? endDate)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var query = _db.Transactions
            .Include(t => t.Card).Include(t => t.Category)
            .Where(t => t.UserId == userId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Merchant.Contains(search));
        if (!string.IsNullOrWhiteSpace(bank)) query = query.Where(t => t.Bank == bank);
        if (!string.IsNullOrWhiteSpace(currency)) query = query.Where(t => t.Currency == currency);
        if (!string.IsNullOrWhiteSpace(movementType)) query = query.Where(t => t.MovementType == movementType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (categoryId.HasValue) query = query.Where(t => t.CategoryId == categoryId);
        if (cardId.HasValue) query = query.Where(t => t.CardId == cardId);
        if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value.AddDays(1));

        var items = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Comercio,Banco,Monto,Moneda,Tipo,Estado,Categoría,Tarjeta,Origen");
        foreach (var t in items)
            sb.AppendLine($"{t.TransactionDate:yyyy-MM-dd},{EscapeCsv(t.Merchant)},{EscapeCsv(t.Bank)},{t.Amount},{t.Currency},{t.MovementType},{t.Status},{EscapeCsv(t.Category?.Name ?? "")},{EscapeCsv(t.Card?.DisplayName ?? "")},{t.Origin}");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"transacciones_{DateTime.Today:yyyyMMdd}.csv");
    }

    private static string EscapeCsv(string v) =>
        v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
