using MimeKit;
using System.Text.RegularExpressions;

namespace PurchaseTracker.Worker.Services.Parsers;

public class BacParser : BaseParser
{
    public override string BankName => "BAC";
    public override string[] SenderDomains => new[] { "baccredomatic.com", "notificaciones.bac" };
    public override string[] SubjectKeywords => new[] { "transaccion", "transacción", "compra", "alerta", "tarjeta" };

    public override List<ParsedPurchase> Parse(MimeMessage msg)
    {
        var results = new List<ParsedPurchase>();
        var body = GetBody(msg);
        var htmlBody = GetHtmlBody(msg);
        var subject = msg.Subject ?? string.Empty;

        // Extract labeled fields from body text
        var merchant = ExtractField(body, htmlBody, new[] { "Comercio:", "Merchant:", "Establecimiento:" });
        var dateStr = ExtractField(body, htmlBody, new[] { "Fecha:", "Date:", "Fecha y hora:" });
        var amountStr = ExtractField(body, htmlBody, new[] { "Monto:", "Amount:", "Importe:", "Valor:" });
        var typeStr = ExtractField(body, htmlBody, new[] { "Tipo:", "Type:", "Tipo de transacción:", "Tipo de movimiento:" });
        var currencyStr = ExtractField(body, htmlBody, new[] { "Moneda:", "Currency:" });

        // Only process COMPRA type transactions
        if (!string.IsNullOrWhiteSpace(typeStr))
        {
            var typeNorm = typeStr.ToUpperInvariant().Trim();
            if (!typeNorm.Contains("COMPRA") && !typeNorm.Contains("PURCHASE"))
                return results;
        }

        if (string.IsNullOrWhiteSpace(merchant) || string.IsNullOrWhiteSpace(amountStr))
            return results;

        // Determine currency
        var currency = "CRC";
        if (!string.IsNullOrWhiteSpace(currencyStr))
        {
            var curr = currencyStr.ToUpperInvariant().Trim();
            if (curr.Contains("USD") || curr.Contains("DOLAR") || curr.Contains("DOLLAR"))
                currency = "USD";
            else if (curr.Contains("CRC") || curr.Contains("COLON") || curr.Contains("COLÓN"))
                currency = "CRC";
        }
        else
        {
            // Try to infer from amount string
            if (amountStr.Contains("$"))
                currency = "USD";
        }

        var amount = CleanAmount(amountStr);
        if (amount <= 0) return results;

        var date = string.IsNullOrWhiteSpace(dateStr) ? DateTime.UtcNow : NormalizeDate(dateStr);
        var last4 = ExtractLast4(subject, body);

        // Handle last4 from body patterns like "terminada en XXXX" or "****XXXX"
        if (last4 == null)
        {
            var m = Regex.Match(body + " " + subject,
                @"\*{4}(\d{4})|terminad[ao]\s+en\s+(\d{4})",
                RegexOptions.IgnoreCase);
            if (m.Success)
                last4 = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        }

        results.Add(new ParsedPurchase(
            Bank: BankName,
            Merchant: merchant.Trim(),
            Amount: amount,
            Currency: currency,
            Date: date,
            Status: "Aprobada",
            CardLast4: last4
        ));

        return results;
    }

    private static string? ExtractField(string plainText, string htmlBody, string[] labels)
    {
        // Try plain text first
        foreach (var label in labels)
        {
            var pattern = Regex.Escape(label) + @"\s*([^\r\n]+)";
            var match = Regex.Match(plainText, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var val = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                    return val;
            }
        }

        // Try HTML (strip tags first)
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(htmlBody);
        var text = doc.DocumentNode.InnerText;

        foreach (var label in labels)
        {
            var pattern = Regex.Escape(label) + @"\s*([^\r\n]+)";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var val = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                    return val;
            }
        }

        return null;
    }
}
