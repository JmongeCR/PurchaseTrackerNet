using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("notes")]
public class NotesController : Controller
{
    private readonly AppDbContext _db;
    public NotesController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(string tab = "todos")
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);

        var notes = await _db.PersonalNotes
            .Include(n => n.Category)
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();

        var categories = await _db.Categories
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var active = notes.Where(n => n.Status != "pagado").ToList();

        var personSummaries = active
            .Where(n => !string.IsNullOrWhiteSpace(n.Person))
            .GroupBy(n => n.Person!)
            .Select(g => new PersonSummary
            {
                Person = g.Key,
                ToCollect = g.Where(n => n.Direction == "cobrar").Sum(n => n.PendingAmount),
                ToPay = g.Where(n => n.Direction == "pagar").Sum(n => n.PendingAmount),
                Count = g.Count()
            })
            .Where(p => p.ToCollect > 0 || p.ToPay > 0)
            .OrderByDescending(p => p.ToCollect + p.ToPay)
            .ToList();

        var vm = new NoteListViewModel
        {
            Notes = notes,
            Categories = categories,
            TotalToCollect = active.Where(n => n.Direction == "cobrar").Sum(n => n.PendingAmount),
            TotalToPay = active.Where(n => n.Direction == "pagar").Sum(n => n.PendingAmount),
            PendingCount = active.Count(n => n.Status == "pendiente"),
            PersonSummaries = personSummaries,
            Tab = tab
        };

        return View(vm);
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddNoteViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || model.Amount <= 0)
        {
            TempData["Error"] = "Completa todos los campos requeridos.";
            return RedirectToAction(nameof(Index));
        }

        var userId = ClaimsHelper.GetUserId(User);

        _db.PersonalNotes.Add(new PersonalNote
        {
            UserId = userId,
            CategoryId = model.CategoryId,
            Title = model.Title,
            Description = model.Description,
            Person = string.IsNullOrWhiteSpace(model.Person) ? null : model.Person,
            Amount = model.Amount,
            Currency = model.Currency,
            NoteDate = DateTime.SpecifyKind(model.NoteDate, DateTimeKind.Utc),
            Direction = model.Direction,
            Notes = model.Notes
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Nota \"{model.Title}\" registrada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditNoteViewModel model)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var note = await _db.PersonalNotes.FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == userId);
        if (note == null) return NotFound();

        note.Title       = model.Title;
        note.Description = model.Description;
        note.Person      = string.IsNullOrWhiteSpace(model.Person) ? null : model.Person;
        note.Amount      = model.Amount;
        note.Currency    = model.Currency;
        note.NoteDate    = DateTime.SpecifyKind(model.NoteDate, DateTimeKind.Utc);
        note.Direction   = model.Direction;
        note.CategoryId  = model.CategoryId;
        note.Notes       = model.Notes;

        // Recalculate status in case amount changed
        if (note.PaidAmount >= note.Amount)      note.Status = "pagado";
        else if (note.PaidAmount > 0)            note.Status = "parcial";
        else                                     note.Status = "pendiente";

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Nota \"{note.Title}\" actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("pay/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPayment(int id, RegisterPaymentViewModel model)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var note = await _db.PersonalNotes.FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == userId);
        if (note == null) return NotFound();

        if (model.PaymentAmount <= 0)
        {
            TempData["Error"] = "El monto del pago debe ser mayor a cero.";
            return RedirectToAction(nameof(Index));
        }

        note.PaidAmount = Math.Min(note.Amount, note.PaidAmount + model.PaymentAmount);
        note.Status = note.PaidAmount >= note.Amount ? "pagado" : "parcial";

        await _db.SaveChangesAsync();

        TempData["Success"] = note.Status == "pagado"
            ? $"✅ \"{note.Title}\" marcada como completamente pagada."
            : $"Pago registrado. Pendiente: {note.Currency} {note.PendingAmount:N0}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var note = await _db.PersonalNotes.FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == userId);
        if (note == null) return NotFound();

        _db.PersonalNotes.Remove(note);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Nota eliminada.";
        return RedirectToAction(nameof(Index));
    }
}
