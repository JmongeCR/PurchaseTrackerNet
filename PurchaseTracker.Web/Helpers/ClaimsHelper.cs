using System.Security.Claims;

namespace PurchaseTracker.Web.Helpers;

public static class ClaimsHelper
{
    public static int GetUserId(ClaimsPrincipal user)
    {
        var val = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(val, out var id) ? id : 0;
    }

    public static string GetUserRole(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? "user";

    public static bool IsAdmin(ClaimsPrincipal user) =>
        GetUserRole(user) == "admin";
}
