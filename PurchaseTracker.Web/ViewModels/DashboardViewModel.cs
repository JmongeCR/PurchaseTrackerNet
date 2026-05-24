using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Web.Services;

namespace PurchaseTracker.Web.ViewModels;

public class DashboardViewModel
{
    public decimal MonthSpendingCrc { get; set; }
    public decimal MonthSpendingUsd { get; set; }
    public int MonthTransactionCount { get; set; }
    public decimal PrevMonthSpendingCrc { get; set; }
    public int ActiveCardsCount { get; set; }
    public int MonthUsdTransactionCount { get; set; }
    public List<Transaction> RecentTransactions { get; set; } = new();
    public List<TopMerchant> TopMerchants { get; set; } = new();
    public List<SpendingByCategory> SpendingByCategory { get; set; } = new();
    public string DailySpendingJson { get; set; } = "[]";
    public string CurrentMonthLabel { get; set; } = string.Empty;

    // Phase 2: Financings & Notes
    public int ActiveFinancingsCount { get; set; }
    public decimal TotalMonthlyCommitment { get; set; }      // CRC
    public decimal TotalPendingFinancing { get; set; }       // CRC
    public decimal TotalMonthlyCommitmentUsd { get; set; }   // USD
    public decimal TotalPendingFinancingUsd { get; set; }    // USD
    public decimal TotalToCollect { get; set; }
    public decimal TotalToPay { get; set; }
    public List<UpcomingPayment> UpcomingPayments { get; set; } = new();

    // Tipo de cambio
    public ExchangeRateData ExchangeRate { get; set; } = ExchangeRateData.Empty;

    // Subscriptions
    public int ActiveSubscriptionsCount { get; set; }
    public decimal TotalMonthlySubscriptionsCrc { get; set; }
    public decimal TotalMonthlySubscriptionsUsd { get; set; }
    public decimal TotalAnnualEstimatedCrc { get; set; }
    public List<UpcomingSubscription> UpcomingSubscriptions { get; set; } = new();

    // Combined upcoming payments (financing + subscriptions)
    public List<DashboardPaymentItem> NextPayments { get; set; } = new();
}

public class TopMerchant
{
    public string Merchant { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class SpendingByCategory
{
    public string CategoryName { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366f1";
    public string Icon { get; set; } = "🏷️";
    public decimal Total { get; set; }
}

public class UpcomingPayment
{
    public string Merchant { get; set; } = string.Empty;
    public DateTime? NextDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    public int RemainingInstallments { get; set; }
}

public class UpcomingSubscription
{
    public string Name { get; set; } = string.Empty;
    public DateTime NextBillingDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    public string Frequency { get; set; } = "mensual";
    public decimal MonthlyEquivalent { get; set; }
}

public class DashboardPaymentItem
{
    public string Name { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    /// <summary>"financing" | "subscription"</summary>
    public string Type { get; set; } = "financing";
    public string? Detail { get; set; }
    public int DaysUntil => (Date.Date - DateTime.UtcNow.Date).Days;
}
