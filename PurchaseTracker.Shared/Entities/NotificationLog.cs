namespace PurchaseTracker.Shared.Entities;

public class NotificationLog
{
    public int NotificationLogId { get; set; }
    public int UserId { get; set; }
    public string Channel { get; set; } = "telegram";
    public string? Message { get; set; }
    public string Status { get; set; } = "sent";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? ErrorDetail { get; set; }

    public User? User { get; set; }
}
