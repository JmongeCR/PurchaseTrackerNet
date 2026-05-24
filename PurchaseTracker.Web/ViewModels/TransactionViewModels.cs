using System.ComponentModel.DataAnnotations;
using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

public class TransactionListViewModel
{
    public List<Transaction> Transactions { get; set; } = new();
    public List<Card> Cards { get; set; } = new();
    public List<Category> Categories { get; set; } = new();

    // Filters
    public string? Search { get; set; }
    public string? Bank { get; set; }
    public string? Currency { get; set; }
    public string? MovementType { get; set; }
    public string? Status { get; set; }
    public int? CategoryId { get; set; }
    public int? CardId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Sort { get; set; } = "date_desc";

    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Totals for filtered view
    public decimal TotalCrc { get; set; }
    public decimal TotalUsd { get; set; }
}

public class EditTransactionViewModel
{
    public int TransactionId { get; set; }
    [Required] public string Bank { get; set; } = string.Empty;
    [Required] public string Merchant { get; set; } = string.Empty;
    [Required][Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
    [Required] public string Currency { get; set; } = "CRC";
    [Required] public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string Status { get; set; } = "Aprobada";
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public int? CardId { get; set; }
    public int? CategoryId { get; set; }
    public string MovementType { get; set; } = "compra";
}

public class AddTransactionViewModel
{
    [Required] public string Bank { get; set; } = string.Empty;
    [Required] public string Merchant { get; set; } = string.Empty;
    [Required][Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
    [Required] public string Currency { get; set; } = "CRC";
    [Required] public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string Status { get; set; } = "Aprobada";
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public int? CardId { get; set; }
    public int? CategoryId { get; set; }
    public string MovementType { get; set; } = "compra";
}
