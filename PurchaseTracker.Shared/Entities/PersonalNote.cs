namespace PurchaseTracker.Shared.Entities;

public class PersonalNote
{
    public int NoteId { get; set; }
    public int UserId { get; set; }
    public int? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Person { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateTime NoteDate { get; set; }
    public string Direction { get; set; } = "cobrar";
    public string Status { get; set; } = "pendiente";
    public decimal PaidAmount { get; set; } = 0;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Category? Category { get; set; }

    public decimal PendingAmount => Amount - PaidAmount;
}
