namespace PurchaseTracker.Shared.Entities;

public class SystemLog
{
    public int SystemLogId { get; set; }
    public string Level { get; set; } = "INFO";
    public string? Source { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
