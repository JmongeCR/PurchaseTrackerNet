using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Shared.Services;
using PurchaseTracker.Web.Helpers;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth")]
[Route("settings")]
public class SettingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordService _pw;
    public SettingsController(AppDbContext db, PasswordService pw)
    {
        _db = db;
        _pw = pw;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (ClaimsHelper.IsAdmin(User))
            return RedirectToAction("Index", "Admin");

        var userId = ClaimsHelper.GetUserId(User);
        var user = await _db.Users.Include(u => u.Settings).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return RedirectToAction("Login", "Auth");

        if (user.Settings == null)
        {
            user.Settings = new UserSettings { UserId = userId };
            _db.UserSettings.Add(user.Settings);
            await _db.SaveChangesAsync();
        }

        var categories = await _db.Categories
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(new SettingsViewModel
        {
            Settings = user.Settings,
            Categories = categories,
            UserFullName = user.FullName,
            UserEmail = user.Email
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(UserSettings model)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null) return NotFound();

        settings.Timezone = string.IsNullOrWhiteSpace(model.Timezone) ? "America/Costa_Rica" : model.Timezone;
        settings.DefaultCurrency = string.IsNullOrWhiteSpace(model.DefaultCurrency) ? "CRC" : model.DefaultCurrency;
        settings.TelegramChatId = model.TelegramChatId;
        settings.TelegramEnabled = model.TelegramEnabled;
        settings.EmailSource = model.EmailSource;
        settings.NotificationsEnabled = model.NotificationsEnabled;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Configuración guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["PwError"] = string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        var userId = ClaimsHelper.GetUserId(User);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return RedirectToAction("Login", "Auth");

        if (!_pw.Verify(model.CurrentPassword, user.PasswordHash))
        {
            TempData["PwError"] = "La contraseña actual es incorrecta.";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = _pw.Hash(model.NewPassword);
        await _db.SaveChangesAsync();

        TempData["PwSuccess"] = "Contraseña actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("categories/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(AddCategoryViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Completa todos los campos de la categoría.";
            return RedirectToAction(nameof(Index));
        }
        var userId = ClaimsHelper.GetUserId(User);
        _db.Categories.Add(new Category
        {
            UserId = userId,
            Name = model.Name,
            Color = model.Color,
            Icon = model.Icon
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Categoría \"{model.Name}\" creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("categories/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id && c.UserId == userId);
        if (cat != null) { cat.IsActive = false; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Categoría eliminada.";
        return RedirectToAction(nameof(Index));
    }
}
