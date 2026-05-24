namespace PurchaseTracker.Shared.Entities;

public class UserSettings
{
    public int UserSettingsId { get; set; }
    public int UserId { get; set; }
    public string? TelegramChatId { get; set; }
    public bool TelegramEnabled { get; set; } = false;
    public string? EmailSource { get; set; }
    public string Timezone { get; set; } = "America/Costa_Rica";
    public string DefaultCurrency { get; set; } = "CRC";
    public bool NotificationsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
