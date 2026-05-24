using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

public class NoteListViewModel
{
    public List<PersonalNote> Notes { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public decimal TotalToCollect { get; set; }
    public decimal TotalToPay { get; set; }
    public int PendingCount { get; set; }
    public List<PersonSummary> PersonSummaries { get; set; } = new();
    public string Tab { get; set; } = "todos";
}

public class PersonSummary
{
    public string Person { get; set; } = string.Empty;
    public decimal ToCollect { get; set; }
    public decimal ToPay { get; set; }
    public int Count { get; set; }
}

public class AddNoteViewModel
{
    public int? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Person { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateTime NoteDate { get; set; } = DateTime.Today;
    public string Direction { get; set; } = "cobrar";
    public string? Notes { get; set; }
}

public class EditNoteViewModel
{
    public int NoteId { get; set; }
    public int? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Person { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CRC";
    public DateTime NoteDate { get; set; } = DateTime.Today;
    public string Direction { get; set; } = "cobrar";
    public string? Notes { get; set; }
}

public class RegisterPaymentViewModel
{
    public int NoteId { get; set; }
    public decimal PaymentAmount { get; set; }
}
