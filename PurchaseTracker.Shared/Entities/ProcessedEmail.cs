namespace PurchaseTracker.Shared.Entities;

public class ProcessedEmail
{
    public int ProcessedEmailId { get; set; }
    public int UserId { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string? ContentHash { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? Source { get; set; }
    public string Status { get; set; } = "ok";

    public User? User { get; set; }
}
