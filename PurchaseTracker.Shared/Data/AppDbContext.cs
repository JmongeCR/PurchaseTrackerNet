using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Shared.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Financing> Financings => Set<Financing>();
    public DbSet<PersonalNote> PersonalNotes => Set<PersonalNote>();
    public DbSet<ATMWithdrawal> ATMWithdrawals => Set<ATMWithdrawal>();
    public DbSet<ProcessedEmail> ProcessedEmails => Set<ProcessedEmail>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // User
        mb.Entity<User>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasMaxLength(50).HasDefaultValue("user");
        });

        // UserSettings
        mb.Entity<UserSettings>(e =>
        {
            e.HasKey(x => x.UserSettingsId);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User)
             .WithOne(x => x.Settings)
             .HasForeignKey<UserSettings>(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Timezone).HasMaxLength(100).HasDefaultValue("America/Costa_Rica");
            e.Property(x => x.DefaultCurrency).HasMaxLength(10).HasDefaultValue("CRC");
        });

        // Card
        mb.Entity<Card>(e =>
        {
            e.HasKey(x => x.CardId);
            e.HasOne(x => x.User)
             .WithMany(x => x.Cards)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Bank).HasMaxLength(100).IsRequired();
            e.Property(x => x.CardAlias).HasMaxLength(200);
            e.Property(x => x.Last4).HasMaxLength(4);
            e.Property(x => x.CardType).HasMaxLength(50).HasDefaultValue("credito");
            e.Property(x => x.CardBrand).HasMaxLength(30);
            e.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("CRC");
            e.Property(x => x.CreditLimit).HasColumnType("decimal(18,2)");
            e.Ignore(x => x.DisplayName);
        });

        // Category
        mb.Entity<Category>(e =>
        {
            e.HasKey(x => x.CategoryId);
            e.HasOne(x => x.User)
             .WithMany(x => x.Categories)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Color).HasMaxLength(20).HasDefaultValue("#6366f1");
            e.Property(x => x.Icon).HasMaxLength(50).HasDefaultValue("tag");
            e.Ignore(x => x.IconEmoji);
        });

        // Financing
        mb.Entity<Financing>(e =>
        {
            e.HasKey(x => x.FinancingId);
            e.HasOne(x => x.User)
             .WithMany(x => x.Financings)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Card)
             .WithMany()
             .HasForeignKey(x => x.CardId)
             .OnDelete(DeleteBehavior.ClientSetNull);
            e.Property(x => x.Merchant).HasMaxLength(200).IsRequired();
            e.Property(x => x.Bank).HasMaxLength(100).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("CRC");
            e.Property(x => x.FinancingType).HasMaxLength(30).HasDefaultValue("cuotas_normales");
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("activo");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.MonthlyPayment).HasColumnType("decimal(18,2)");
            e.Property(x => x.InterestRate).HasColumnType("decimal(8,4)");
            e.Property(x => x.Commission).HasColumnType("decimal(18,2)");
            e.Ignore(x => x.RemainingInstallments);
            e.Ignore(x => x.PendingAmount);
            e.Ignore(x => x.NextPaymentDate);
        });

        // PersonalNote
        mb.Entity<PersonalNote>(e =>
        {
            e.HasKey(x => x.NoteId);
            e.HasOne(x => x.User)
             .WithMany(x => x.PersonalNotes)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category)
             .WithMany()
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.ClientSetNull);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Person).HasMaxLength(100);
            e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("CRC");
            e.Property(x => x.Direction).HasMaxLength(10).HasDefaultValue("cobrar");
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("pendiente");
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
            e.Ignore(x => x.PendingAmount);
        });

        // ATMWithdrawal
        mb.Entity<ATMWithdrawal>(e =>
        {
            e.HasKey(x => x.WithdrawalId);
            e.HasOne(x => x.User)
             .WithMany(x => x.ATMWithdrawals)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Card)
             .WithMany()
             .HasForeignKey(x => x.CardId)
             .OnDelete(DeleteBehavior.ClientSetNull);
            e.Property(x => x.Bank).HasMaxLength(100).IsRequired();
            e.Property(x => x.ATMLocation).HasMaxLength(300);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Fee).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            e.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("CRC");
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.WithdrawalDate);
        });

        // ProcessedEmail
        mb.Entity<ProcessedEmail>(e =>
        {
            e.HasKey(x => x.ProcessedEmailId);
            e.HasOne(x => x.User)
             .WithMany(x => x.ProcessedEmails)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.MessageId).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.MessageId).IsUnique();   // evita procesar el mismo email dos veces
            e.Property(x => x.ContentHash).HasMaxLength(100);
            e.Property(x => x.Source).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("ok");
        });

        // NotificationLog
        mb.Entity<NotificationLog>(e =>
        {
            e.HasKey(x => x.NotificationLogId);
            e.HasOne(x => x.User)
             .WithMany(x => x.NotificationLogs)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Channel).HasMaxLength(50).HasDefaultValue("telegram");
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("sent");
            e.Property(x => x.ErrorDetail).HasMaxLength(1000);
            e.HasIndex(x => x.SentAt);
        });

        // SystemLog (tabla global, sin FK a User)
        mb.Entity<SystemLog>(e =>
        {
            e.HasKey(x => x.SystemLogId);
            e.Property(x => x.Level).HasMaxLength(20).HasDefaultValue("INFO");
            e.Property(x => x.Source).HasMaxLength(100);
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Details).HasColumnType("nvarchar(max)");
            e.HasIndex(x => x.CreatedAt);
        });

        // Subscription
        mb.Entity<Subscription>(e =>
        {
            e.HasKey(x => x.SubscriptionId);
            e.HasOne(x => x.User)
             .WithMany(x => x.Subscriptions)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Card)
             .WithMany()
             .HasForeignKey(x => x.CardId)
             .OnDelete(DeleteBehavior.ClientSetNull);
            e.HasOne(x => x.Category)
             .WithMany()
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.ClientSetNull);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Merchant).HasMaxLength(200);
            e.Property(x => x.Bank).HasMaxLength(100);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("CRC");
            e.Property(x => x.Frequency).HasMaxLength(20).HasDefaultValue("mensual");
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("activa");
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => x.NextBillingDate);
            e.Ignore(x => x.MonthlyEquivalent);
            e.Ignore(x => x.DaysUntilBilling);
            e.Ignore(x => x.FrequencyLabel);
        });

        // Transaction
        mb.Entity<Transaction>(e =>
        {
            e.HasKey(x => x.TransactionId);
            e.HasOne(x => x.User)
             .WithMany(x => x.Transactions)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Card)
             .WithMany(x => x.Transactions)
             .HasForeignKey(x => x.CardId)
             .OnDelete(DeleteBehavior.ClientSetNull);   // NO ACTION en BD, EF Core anula en memoria
            e.HasOne(x => x.Category)
             .WithMany(x => x.Transactions)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.ClientSetNull);   // NO ACTION en BD, EF Core anula en memoria
            e.Property(x => x.Bank).HasMaxLength(100).IsRequired();
            e.Property(x => x.Merchant).HasMaxLength(500).IsRequired();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("CRC");
            e.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("Aprobada");
            e.Property(x => x.MovementType).HasMaxLength(50).HasDefaultValue("compra");
            e.Property(x => x.Origin).HasMaxLength(10).HasDefaultValue("manual");
            e.HasIndex(x => x.TransactionDate);
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(ct);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);
        foreach (var entry in entries)
        {
            if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            if (entry.State == EntityState.Added &&
                entry.Properties.Any(p => p.Metadata.Name == "CreatedAt"))
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }
    }
}
