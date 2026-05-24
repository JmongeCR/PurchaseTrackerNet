namespace PurchaseTracker.Shared.Entities;

public class Transaction
{
    public int TransactionId { get; set; }
    public int UserId { get; set; }
    public int? CardId { get; set; }
    public int? CategoryId { get; set; }
    public string MovementType { get; set; } = "compra";
    public string Bank { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Aprobada";
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string Origin { get; set; } = "manual";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Card? Card { get; set; }
    public Category? Category { get; set; }
}
