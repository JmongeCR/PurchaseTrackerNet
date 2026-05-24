using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

public class SubscriptionListViewModel
{
    public List<Subscription> Activas   { get; set; } = new();
    public List<Subscription> Pausadas  { get; set; } = new();
    public List<Subscription> Canceladas { get; set; } = new();

    public int    ActiveCount                { get; set; }
    public decimal TotalMonthlyCrc           { get; set; }
    public decimal TotalMonthlyUsd           { get; set; }
    public decimal TotalAnnualCrc            { get; set; }
    public decimal TotalAnnualUsd            { get; set; }

    public List<Card>     Cards      { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string Tab { get; set; } = "activas";
}

public class AddSubscriptionViewModel
{
    public string Name        { get; set; } = string.Empty;
    public string? Merchant   { get; set; }
    public string? Bank       { get; set; }
    public int?   CardId      { get; set; }
    public int?   CategoryId  { get; set; }
    public decimal Amount     { get; set; }
    public string Currency    { get; set; } = "CRC";
    public string Frequency   { get; set; } = "mensual";
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime NextBillingDate { get; set; } = DateTime.UtcNow.Date;
    public string? Notes      { get; set; }
}

public class EditSubscriptionViewModel
{
    public string Name        { get; set; } = string.Empty;
    public string? Merchant   { get; set; }
    public string? Bank       { get; set; }
    public int?   CardId      { get; set; }
    public int?   CategoryId  { get; set; }
    public decimal Amount     { get; set; }
    public string Currency    { get; set; } = "CRC";
    public string Frequency   { get; set; } = "mensual";
    public DateTime StartDate { get; set; }
    public DateTime NextBillingDate { get; set; }
    public string? Notes      { get; set; }
}
