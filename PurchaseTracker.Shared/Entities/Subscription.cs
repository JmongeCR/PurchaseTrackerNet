namespace PurchaseTracker.Shared.Entities;

public class Subscription
{
    public int SubscriptionId { get; set; }
    public int UserId { get; set; }
    public int? CardId { get; set; }
    public int? CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public string? Bank { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";

    /// <summary>mensual | anual | trimestral | semanal</summary>
    public string Frequency { get; set; } = "mensual";

    public DateTime StartDate { get; set; }
    public DateTime NextBillingDate { get; set; }

    /// <summary>activa | pausada | cancelada</summary>
    public string Status { get; set; } = "activa";

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Card? Card { get; set; }
    public Category? Category { get; set; }

    // Computed — ignored by EF Core
    public decimal MonthlyEquivalent => Frequency switch
    {
        "anual"      => Math.Round(Amount / 12, 2),
        "trimestral" => Math.Round(Amount / 3,  2),
        "semanal"    => Math.Round(Amount * 4.33m, 2),
        _            => Amount   // mensual
    };

    public int DaysUntilBilling => (NextBillingDate.Date - DateTime.UtcNow.Date).Days;

    public string FrequencyLabel => Frequency switch
    {
        "anual"      => "Anual",
        "trimestral" => "Trimestral",
        "semanal"    => "Semanal",
        _            => "Mensual"
    };
}
