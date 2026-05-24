using MimeKit;
using System.Text.RegularExpressions;

namespace PurchaseTracker.Worker.Services.Parsers;

public class BcrParser : BaseParser
{
    public override string BankName => "BCR";
    public override string[] SenderDomains => new[] { "bancobcr.com", "notificacionesbcr" };
    public override string[] SubjectKeywords => new[] { "transaccion", "transacción", "tarjeta bcr", "debito", "débito" };

    // Match table rows from BCR HTML emails:
    // Date/time | Amount | Currency | Merchant | Status
    private static readonly Regex RowPattern = new(
        @"(\d{2}/\d{2}/\d{4})\s+\d{2}:\d{2}:\d{2}.*?" +
        @"([\d\.,]+)\s+" +
        @"(COLON[^<\r\n]*|DOLARES?[^<\r\n]*|DOLAR[^<\r\n]*)\s+" +
        @"([^<\r\n]+?)\s+" +
        @"(Aprobada|Rechazada|Denegada)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase
    );

    private static readonly Dictionary<string, string> CurrencyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["COLON COSTA RICA"] = "CRC",
        ["COLON"] = "CRC",
        ["COLONES"] = "CRC",
        ["DOLAR"] = "USD",
        ["DOLARES"] = "USD",
        ["DOLLAR"] = "USD",
        ["DOLLARS"] = "USD"
    };

    public override List<ParsedPurchase> Parse(MimeMessage msg)
    {
        var results = new List<ParsedPurchase>();
        var htmlBody = GetHtmlBody(msg);
        var textBody = GetBody(msg);
        var subject = msg.Subject ?? string.Empty;

        var last4 = ExtractLast4(subject, textBody);

        // Try HTML rows first
        var matches = RowPattern.Matches(htmlBody);

        if (matches.Count == 0)
        {
            // Fallback: try on plain text body
            matches = RowPattern.Matches(textBody);
        }

        foreach (Match match in matches)
        {
            var dateStr = match.Groups[1].Value.Trim();
            var amountStr = match.Groups[2].Value.Trim();
            var currencyRaw = match.Groups[3].Value.Trim().ToUpperInvariant();
            var merchant = CleanHtml(match.Groups[4].Value.Trim());
            var status = match.Groups[5].Value.Trim();

            // Normalize currency
            var currency = "CRC";
            foreach (var kvp in CurrencyMap)
            {
                if (currencyRaw.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    currency = kvp.Value;
                    break;
                }
            }

            var date = NormalizeDate(dateStr);
            var amount = CleanAmount(amountStr);

            if (amount <= 0 || string.IsNullOrWhiteSpace(merchant))
                continue;

            results.Add(new ParsedPurchase(
                Bank: BankName,
                Merchant: merchant,
                Amount: amount,
                Currency: currency,
                Date: date,
                Status: status,
                CardLast4: last4
            ));
        }

        return results;
    }

    private static string CleanHtml(string input)
    {
        // Remove HTML tags
        input = Regex.Replace(input, @"<[^>]+>", " ");
        // Decode HTML entities
        input = System.Net.WebUtility.HtmlDecode(input);
        // Collapse whitespace
        input = Regex.Replace(input, @"\s+", " ").Trim();
        return input;
    }
}
