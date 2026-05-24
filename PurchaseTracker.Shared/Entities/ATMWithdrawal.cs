namespace PurchaseTracker.Shared.Entities;

public class ATMWithdrawal
{
    public int WithdrawalId { get; set; }
    public int UserId { get; set; }
    public int? CardId { get; set; }
    public string Bank { get; set; } = string.Empty;
    public string? ATMLocation { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateTime WithdrawalDate { get; set; }
    public decimal Fee { get; set; } = 0;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Card? Card { get; set; }
}
