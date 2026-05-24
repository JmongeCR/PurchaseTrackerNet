namespace PurchaseTracker.Shared.Entities;

public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Role { get; set; } = "user";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public UserSettings? Settings { get; set; }
    public ICollection<Card> Cards { get; set; } = new List<Card>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Financing> Financings { get; set; } = new List<Financing>();
    public ICollection<PersonalNote> PersonalNotes { get; set; } = new List<PersonalNote>();
    public ICollection<ATMWithdrawal> ATMWithdrawals { get; set; } = new List<ATMWithdrawal>();
    public ICollection<ProcessedEmail> ProcessedEmails { get; set; } = new List<ProcessedEmail>();
    public ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
