using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Shared.Services;
using PurchaseTracker.Web.Services;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Controllers;

[Authorize(AuthenticationSchemes = "PTAuth", Roles = "admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordService _pw;
    private readonly EmailService _email;

    public AdminController(AppDbContext db, PasswordService pw, EmailService email)
    {
        _db    = db;
        _pw    = pw;
        _email = email;
    }

    // ── GET /admin ────────────────────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = await _db.Users
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var vm = new AdminViewModel
        {
            Users       = users,
            TotalUsers  = users.Count,
            ActiveUsers = users.Count(u => u.IsActive)
        };
        return View(vm);
    }

    // ── POST /admin/users/create ──────────────────────────────────────────────
    [HttpPost("users/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" | ", ModelState.Values
                .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Users.AnyAsync(u => u.Email == model.Email))
        {
            TempData["Error"] = $"Ya existe un usuario con el correo '{model.Email}'.";
            return RedirectToAction(nameof(Index));
        }

        var user = new User
        {
            FullName     = model.FullName,
            Email        = model.Email,
            PasswordHash = _pw.Hash(model.Password),
            Role         = model.Role,
            IsActive     = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.UserSettings.Add(new UserSettings { UserId = user.UserId });
        await _db.SaveChangesAsync();

        if (model.SendWelcomeEmail)
            await _email.SendWelcomeEmailAsync(user.Email, user.FullName, model.Password);

        TempData["Success"] = $"Usuario '{user.FullName}' creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /admin/users/{id}/toggle ─────────────────────────────────────────
    [HttpPost("users/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            TempData["Error"] = "Usuario no encontrado.";
            return RedirectToAction(nameof(Index));
        }
        if (user.Email == "admin@purchasetracker.local")
        {
            TempData["Error"] = "No se puede desactivar el administrador del sistema.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Usuario '{user.FullName}' {(user.IsActive ? "activado" : "desactivado")}.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /admin/users/{id}/reset-password ─────────────────────────────────
    [HttpPost("users/{id:int}/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            TempData["Error"] = "La contraseña debe tener al menos 8 caracteres.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            TempData["Error"] = "Usuario no encontrado.";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = _pw.Hash(newPassword);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Contraseña de '{user.FullName}' restablecida.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /admin/users/{id}/delete ─────────────────────────────────────────
    [HttpPost("users/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            TempData["Error"] = "Usuario no encontrado.";
            return RedirectToAction(nameof(Index));
        }
        if (user.Email == "admin@purchasetracker.local")
        {
            TempData["Error"] = "No se puede eliminar el administrador del sistema.";
            return RedirectToAction(nameof(Index));
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Usuario '{user.FullName}' eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
