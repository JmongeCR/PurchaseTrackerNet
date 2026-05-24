using MimeKit;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PurchaseTracker.Worker.Services.Parsers;

public record ParsedPurchase(
    string Bank,
    string Merchant,
    decimal Amount,
    string Currency,
    DateTime Date,
    string Status,
    string? CardLast4
);

public abstract class BaseParser
{
    public abstract string BankName { get; }
    public abstract string[] SenderDomains { get; }
    public abstract string[] SubjectKeywords { get; }

    public virtual bool CanParse(MimeMessage msg)
    {
        var from = msg.From.ToString().ToLowerInvariant();
        var subject = (msg.Subject ?? string.Empty).ToLowerInvariant();

        var domainMatch = SenderDomains.Any(d => from.Contains(d.ToLowerInvariant()));
        var subjectMatch = SubjectKeywords.Any(k => subject.Contains(k.ToLowerInvariant()));

        return domainMatch || subjectMatch;
    }

    public abstract List<ParsedPurchase> Parse(MimeMessage msg);

    protected string GetBody(MimeMessage msg)
    {
        if (!string.IsNullOrWhiteSpace(msg.TextBody))
            return msg.TextBody;

        if (!string.IsNullOrWhiteSpace(msg.HtmlBody))
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(msg.HtmlBody);
            // Remove script and style nodes
            foreach (var node in doc.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlAgilityPack.HtmlNode>())
                node.Remove();
            return doc.DocumentNode.InnerText;
        }

        return string.Empty;
    }

    protected string GetHtmlBody(MimeMessage msg)
    {
        return msg.HtmlBody ?? msg.TextBody ?? string.Empty;
    }

    protected decimal CleanAmount(string raw)
    {
        // Remove currency symbols, spaces
        raw = raw.Trim();
        raw = Regex.Replace(raw, @"[₡$\s]", "");

        // Normalize: if comma is decimal separator (e.g., CRC: 1.234,56 or 1,234.56)
        // Heuristic: if last separator is comma, it's decimal
        if (raw.Contains(',') && raw.Contains('.'))
        {
            var lastComma = raw.LastIndexOf(',');
            var lastDot = raw.LastIndexOf('.');
            if (lastComma > lastDot)
            {
                // European format: 1.234,56
                raw = raw.Replace(".", "").Replace(",", ".");
            }
            else
            {
                // US format: 1,234.56
                raw = raw.Replace(",", "");
            }
        }
        else if (raw.Contains(','))
        {
            // Could be thousands separator or decimal
            var parts = raw.Split(',');
            if (parts.Length == 2 && parts[1].Length <= 2)
                raw = raw.Replace(",", ".");
            else
                raw = raw.Replace(",", "");
        }

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0m;
    }

    protected DateTime NormalizeDate(string raw)
    {
        raw = raw.Trim();

        // Try common formats
        var formats = new[]
        {
            "dd/MM/yyyy", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm",
            "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy", "dd-MM-yyyy",
            "dd/MM/yyyy hh:mm:ss tt",
            "d/M/yyyy", "d/M/yyyy H:mm:ss"
        };

        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(raw, fmt, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
                return dt;
        }

        if (DateTime.TryParse(raw, out var fallback))
            return fallback;

        return DateTime.UtcNow;
    }

    protected string? ExtractLast4(string subject, string body)
    {
        var combined = subject + " " + body;

        // Patterns: ****-****-****-8896, ****8896, terminada en 8896, termina en 8896
        var patterns = new[]
        {
            @"\*{4}[\-\s]?\*{4}[\-\s]?\*{4}[\-\s]?(\d{4})",
            @"\*{4}(\d{4})",
            @"termina(?:da)?\s+en\s+(\d{4})",
            @"terminaci[oó]n\s+(\d{4})",
            @"(?:tarjeta|card).*?(\d{4})\b"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(combined, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }
}
