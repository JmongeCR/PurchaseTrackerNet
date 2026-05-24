namespace PurchaseTracker.Shared.Entities;

public class Financing
{
    public int FinancingId { get; set; }
    public int UserId { get; set; }
    public int? CardId { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public string Bank { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateTime PurchaseDate { get; set; }
    public string FinancingType { get; set; } = "cuotas_normales";
    public int Months { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal? InterestRate { get; set; }
    public decimal? Commission { get; set; }
    public DateTime StartDate { get; set; }
    public int PaidInstallments { get; set; } = 0;
    public string Status { get; set; } = "activo";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Card? Card { get; set; }

    public int RemainingInstallments => Math.Max(0, Months - PaidInstallments);
    public decimal PendingAmount => RemainingInstallments * MonthlyPayment;

    public DateTime? NextPaymentDate
    {
        get
        {
            if (Status != "activo" || RemainingInstallments == 0) return null;
            // StartDate = date of installment #1, so installment #(PaidInstallments+1)
            // falls on StartDate + PaidInstallments months (no +1 needed)
            return StartDate.AddMonths(PaidInstallments);
        }
    }
}
