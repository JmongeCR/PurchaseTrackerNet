using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Worker.Services;

public class ImapResult
{
    public int ProcessedCount { get; set; }
    public int SavedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
}

public class ImapService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImapService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmlProcessorService _processor;

    public ImapService(
        IConfiguration configuration,
        ILogger<ImapService> logger,
        IServiceScopeFactory scopeFactory,
        EmlProcessorService processor)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _processor = processor;
    }

    public async Task<ImapResult> ProcessInboxAsync(CancellationToken cancellationToken = default)
    {
        var result = new ImapResult();

        var host = _configuration["Imap:Host"];
        var port = _configuration.GetValue<int>("Imap:Port", 993);
        var user = _configuration["Imap:User"];
        var pass = _configuration["Imap:Pass"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            _logger.LogWarning("IMAP not configured, skipping inbox poll");
            return result;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var client = new ImapClient();

        try
        {
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.SslOnConnect, cancellationToken);
            await client.AuthenticateAsync(user, pass, cancellationToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Fetch unseen messages
            var uids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
            _logger.LogInformation("Found {Count} unseen messages", uids.Count);

            // Load all users with their settings for routing
            var users = await db.Users
                .Include(u => u.Settings)
                .Where(u => u.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var uid in uids)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var message = await inbox.GetMessageAsync(uid, cancellationToken);
                    result.ProcessedCount++;

                    // Extract message ID
                    var messageId = message.MessageId ?? uid.ToString();

                    // Check for dedup across all users
                    var alreadyProcessed = await db.ProcessedEmails
                        .AnyAsync(p => p.MessageId == messageId, cancellationToken);

                    if (alreadyProcessed)
                    {
                        _logger.LogDebug("Message {Id} already processed, skipping", messageId);
                        result.SkippedCount++;
                        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                        continue;
                    }

                    // Identify user by email routing
                    var targetUserId = IdentifyUser(message, users);

                    if (targetUserId == null)
                    {
                        _logger.LogWarning("No user found for message {Id} ({Subject})", messageId, message.Subject);
                        result.SkippedCount++;
                        continue;
                    }

                    var processResult = await _processor.ProcessMessageAsync(message, targetUserId.Value, db);

                    if (processResult.Status == "saved")
                    {
                        result.SavedCount += processResult.Count;
                        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                    }
                    else if (processResult.Status == "no_parser")
                    {
                        // Still mark as seen to avoid reprocessing
                        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                        result.SkippedCount++;
                    }
                    else
                    {
                        result.ErrorCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message UID {Uid}", uid);
                    result.ErrorCount++;
                }
            }

            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("IMAP processing cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IMAP connection/processing error");
            result.ErrorCount++;
        }

        return result;
    }

    private static int? IdentifyUser(MimeMessage message, List<User> users)
    {
        // Extract all destination addresses from headers
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var addr in message.To.Mailboxes)
            destinations.Add(addr.Address);

        foreach (var addr in message.Cc.Mailboxes)
            destinations.Add(addr.Address);

        // Check X-Original-To and Delivered-To headers
        foreach (var header in message.Headers)
        {
            if (header.Field.Equals("X-Original-To", StringComparison.OrdinalIgnoreCase) ||
                header.Field.Equals("Delivered-To", StringComparison.OrdinalIgnoreCase))
            {
                destinations.Add(header.Value.Trim());
            }
        }

        // Match against UserSettings.EmailSource and User.Email
        foreach (var user in users)
        {
            // Check EmailSource first (preferred)
            if (user.Settings?.EmailSource != null &&
                destinations.Contains(user.Settings.EmailSource))
                return user.UserId;

            // Fallback to user email
            if (destinations.Contains(user.Email))
                return user.UserId;
        }

        // Last resort: if only one user exists, assign to them
        if (users.Count == 1) return users[0].UserId;

        return null;
    }
}
