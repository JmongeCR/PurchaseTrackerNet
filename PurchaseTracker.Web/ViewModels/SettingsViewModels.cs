using System.ComponentModel.DataAnnotations;
using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

public class SettingsViewModel
{
    public UserSettings Settings { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Ingresa tu contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la nueva contraseña")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la nueva contraseña")]
    [Compare("NewPassword", ErrorMessage = "Las contraseñas no coinciden")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AddCategoryViewModel
{
    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required] public string Color { get; set; } = "#6366f1";
    [Required] public string Icon { get; set; } = "tag";
}
