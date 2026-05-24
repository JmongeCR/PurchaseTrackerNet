using System.ComponentModel.DataAnnotations;
using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

public class AdminViewModel
{
    public List<User> Users  { get; set; } = new();
    public int TotalUsers    { get; set; }
    public int ActiveUsers   { get; set; }
}

public class CreateUserViewModel
{
    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es requerido")]
    [EmailAddress(ErrorMessage = "Correo inválido")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "user";
    public bool SendWelcomeEmail { get; set; } = true;
}

public class ResetPasswordViewModel
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Ingresa la nueva contraseña")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
    public string NewPassword { get; set; } = string.Empty;
}
