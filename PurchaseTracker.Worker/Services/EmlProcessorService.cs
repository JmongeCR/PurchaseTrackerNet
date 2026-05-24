using Microsoft.EntityFrameworkCore;
using MimeKit;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;
using PurchaseTracker.Shared.Services;
using PurchaseTracker.Worker.Services.Parsers;
using System.Security.Cryptography;

namespace PurchaseTracker.Worker.Services;

public class ProcessResult
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public string? Detail { get; set; }
}

public class EmlProcessorService
{
    private readonly ILogger<EmlProcessorService> _logger;
    private readonly List<BaseParser> _parsers;
    private readonly TelegramService _telegramService;

    public EmlProcessorService(ILogger<EmlProcessorService> logger, TelegramService telegramService)
    {
        _logger = logger;
        _telegramService = telegramService;
        _parsers = new List<BaseParser>
        {
            new BcrParser(),
            new BacParser()
        };
    }

    public async Task<ProcessResult> ProcessMessageAsync(MimeMessage message, int userId, AppDbContext db)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(message));
        if (parser == null)
        {
            _logger.LogWarning("No parser found for message: {Subject}", message.Subject);
            return new ProcessResult { Status = "no_parser" };
        }

        List<ParsedPurchase> purchases;
        try
        {
            purchases = parser.Parse(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parser error for {Bank}", parser.BankName);
            return new ProcessResult { Status = "error", Detail = ex.Message };
        }

        if (!purchases.Any())
        {
            _logger.LogInformation("Parser found no purchases in message");
            return new ProcessResult { Status = "no_purchases" };
        }

        var saved = 0;
        var notifications = new List<string>();

        foreach (var purchase in purchases)
        {
            if (purchase.Status.Equals("Rechazada", StringComparison.OrdinalIgnoreCase) ||
                purchase.Status.Equals("Denegada", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Skipping rejected purchase: {Merchant}", purchase.Merchant);
                continue;
            }

            var cardId = await ResolveCardId(userId, purchase.Bank, purchase.CardLast4, db);

            var tx = new Transaction
            {
                UserId = userId,
                CardId = cardId,
                Bank = purchase.Bank,
                Merchant = purchase.Merchant,
                Amount = purchase.Amount,
                Currency = purchase.Currency,
                TransactionDate = purchase.Date,
                Status = purchase.Status,
                Origin = "auto",
                ExternalMessageId = message.MessageId,
                RawSubject = message.Subject,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Transactions.Add(tx);
            saved++;

            notifications.Add($"💳 {purchase.Merchant}: {purchase.Currency} {purchase.Amount:N0}");
        }

        if (saved > 0)
        {
            // Record processed email
            var rawBytes = Array.Empty<byte>();
            using var ms = new MemoryStream();
            await message.WriteToAsync(ms);
            rawBytes = ms.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(rawBytes));

            db.ProcessedEmails.Add(new ProcessedEmail
            {
                UserId = userId,
                MessageId = message.MessageId ?? Guid.NewGuid().ToString(),
                ContentHash = hash,
                ProcessedAt = DateTime.UtcNow,
                Source = "imap",
                Status = "ok"
            });

            await db.SaveChangesAsync();

            // Send Telegram notification
            if (notifications.Any())
            {
                var notifMsg = $"📧 <b>Nuevas transacciones ({saved})</b>\n" +
                               string.Join("\n", notifications);
                await _telegramService.SendAsync(userId, notifMsg);
            }
        }

        return new ProcessResult { Status = "saved", Count = saved };
    }

    public async Task<int?> ResolveCardId(int userId, string bank, string? last4, AppDbContext db)
    {
        if (!string.IsNullOrWhiteSpace(last4))
        {
            var card = await db.Cards.FirstOrDefaultAsync(c =>
                c.UserId == userId &&
                c.Bank == bank &&
                c.Last4 == last4 &&
                c.IsActive);
            if (card != null) return card.CardId;
        }

        // If no last4 provided, try to find single active card for this bank
        var cards = await db.Cards
            .Where(c => c.UserId == userId && c.Bank == bank && c.IsActive)
            .ToListAsync();

        if (cards.Count == 1) return cards[0].CardId;

        return null;
    }

    public async Task<List<ProcessResult>> ProcessMimeMessageBytesAsync(byte[] rawBytes, int userId, AppDbContext db)
    {
        var results = new List<ProcessResult>();

        using var ms = new MemoryStream(rawBytes);
        MimeMessage message;
        try
        {
            message = await MimeMessage.LoadAsync(ms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse MimeMessage from bytes");
            return new List<ProcessResult> { new ProcessResult { Status = "error", Detail = ex.Message } };
        }

        // Check if it's a multipart with message/rfc822 attachments
        if (message.Body is MimeKit.Multipart multipart)
        {
            foreach (var part in multipart)
            {
                if (part is MimePart mp && mp.ContentType.MimeType.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase))
                {
                    using var innerMs = new MemoryStream();
                    await mp.Content.DecodeToAsync(innerMs);
                    innerMs.Position = 0;
                    var innerMsg = await MimeMessage.LoadAsync(innerMs);
                    var res = await ProcessMessageAsync(innerMsg, userId, db);
                    results.Add(res);
                }
            }
        }

        if (!results.Any())
        {
            var res = await ProcessMessageAsync(message, userId, db);
            results.Add(res);
        }

        return results;
    }
}
