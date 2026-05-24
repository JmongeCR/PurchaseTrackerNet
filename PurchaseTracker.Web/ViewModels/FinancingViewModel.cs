using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

public class FinancingListViewModel
{
    public List<Financing> Activos { get; set; } = new();
    public List<Financing> Pagados { get; set; } = new();
    public List<Financing> Cancelados { get; set; } = new();
    public int ActiveCount { get; set; }
    // CRC
    public decimal TotalMonthlyCommitment { get; set; }
    public decimal TotalPendingAmount { get; set; }
    // USD
    public decimal TotalMonthlyCommitmentUsd { get; set; }
    public decimal TotalPendingAmountUsd { get; set; }
    public List<Card> Cards { get; set; } = new();
    public string Tab { get; set; } = "activos";
}

public class AddFinancingViewModel
{
    public int? CardId { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public string Bank { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public string FinancingType { get; set; } = "cuotas_normales";
    public int Months { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal? InterestRate { get; set; }
    public decimal? Commission { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
}

public class EditFinancingViewModel : AddFinancingViewModel
{
    public int FinancingId { get; set; }
    public int PaidInstallments { get; set; }
    public string Status { get; set; } = "activo";
}
