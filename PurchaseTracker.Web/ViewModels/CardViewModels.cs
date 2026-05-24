using System.ComponentModel.DataAnnotations;
using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

public class CardListViewModel
{
    public List<CardWithStats> Cards { get; set; } = new();

    // Cycle tab summary KPIs
    public decimal TotalToPayCrc { get; set; }
    public decimal TotalOpenCycleCrc { get; set; }
    public int ActiveCreditCardsWithCycle { get; set; }
}

public class CardWithStats
{
    public Card Card { get; set; } = null!;
    public decimal MonthSpendingCrc { get; set; }
    public decimal MonthSpendingUsd { get; set; }
    public int MonthTransactionCount { get; set; }
    public DateTime? LastTransactionDate { get; set; }

    // Credit cycle — dates
    public DateTime? LastCutDate { get; set; }      // Most recent cut that already happened
    public DateTime? PrevCutDate { get; set; }      // Cut before LastCutDate (start of closed cycle)
    public DateTime? NextCutDate { get; set; }      // Upcoming cut
    public DateTime? PaymentDueDate { get; set; }   // Payment due for closed cycle
    public int DaysUntilCut { get; set; }

    // Credit cycle — amounts
    public decimal LastCycleSpending { get; set; }  // Closed cycle: PrevCut → LastCut (to pay)
    public int LastCycleTxCount { get; set; }
    public decimal OpenCycleSpending { get; set; }  // Open cycle: LastCut → now (accumulating)
    public int OpenCycleTxCount { get; set; }

    // Keep for backwards compat (open cycle = CycleSpending)
    public decimal CycleSpending => OpenCycleSpending;
    public DateTime? NextPaymentDate => PaymentDueDate;
}

public class AddCardViewModel
{
    [Required(ErrorMessage = "El banco es requerido")]
    public string Bank { get; set; } = string.Empty;
    public string? CardAlias { get; set; }
    [StringLength(4, MinimumLength = 4, ErrorMessage = "Exactamente 4 dígitos")]
    public string? Last4 { get; set; }
    [Required] public string CardType { get; set; } = "credito";
    public string? CardBrand { get; set; }
    [Required] public string Currency { get; set; } = "CRC";
    public int? CutDay { get; set; }
    public int? PaymentDays { get; set; }
    public decimal? CreditLimit { get; set; }
}

public class EditCardViewModel : AddCardViewModel
{
    public int CardId { get; set; }
}
