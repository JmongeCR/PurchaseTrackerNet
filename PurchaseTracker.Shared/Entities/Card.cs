namespace PurchaseTracker.Shared.Entities;

public class Card
{
    public int CardId { get; set; }
    public int UserId { get; set; }
    public string Bank { get; set; } = string.Empty;
    public string? CardAlias { get; set; }
    public string? Last4 { get; set; }
    public string CardType { get; set; } = "credito";
    public string? CardBrand { get; set; }              // "Visa", "Mastercard", "Amex", "EFTPOS", etc.
    public string Currency { get; set; } = "CRC";
    public bool IsActive { get; set; } = true;
    public int? CutDay { get; set; }
    public int? PaymentDays { get; set; }
    public decimal? CreditLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public string DisplayName => CardAlias ?? $"{Bank} {(Last4 != null ? $"****{Last4}" : "")}".Trim();
}
