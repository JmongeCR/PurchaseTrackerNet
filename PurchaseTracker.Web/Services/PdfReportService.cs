using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Services;

public class PdfReportService
{
    // ── Palette ───────────────────────────────────────────────────────────
    private const string CHeader  = "#0f172a";   // dark slate — header bg
    private const string CPrimary = "#1e40af";   // indigo
    private const string CAlt     = "#f8fafc";   // table row alt
    private const string CBorder  = "#e2e8f0";
    private const string CMuted   = "#64748b";
    private const string CGreen   = "#16a34a";
    private const string CViolet  = "#7c3aed";   // financing amounts
    private const string CWhite   = "#ffffff";
    private const string CInfoBar = "#bfdbfe";   // header accent text
    private const string CText    = "#0f172a";

    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    public byte[] Generate(ReportData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginHorizontal(1.4f, Unit.Centimetre);
                page.MarginVertical(1.2f,   Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9).FontColor(CText));

                page.Header().Element(c => DrawHeader(c, data));
                page.Content().PaddingTop(10).Element(c => DrawBody(c, data));
                page.Footer().Element(DrawFooter);
            });
        }).GeneratePdf();
    }

    // ══ HEADER ════════════════════════════════════════════════════════════
    private static void DrawHeader(IContainer c, ReportData d)
    {
        c.Column(col =>
        {
            // Title band
            col.Item().Background(CHeader).Padding(14).Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("💳  PurchaseTracker")
                         .Bold().FontSize(15).FontColor(CWhite);
                    inner.Item().Text("Reporte financiero personal")
                         .FontSize(8).FontColor("#94a3b8");
                });
                row.AutoItem().AlignRight().AlignMiddle().Column(inner =>
                {
                    inner.Item().AlignRight()
                         .Text(d.Filter.SourceLabel)
                         .Bold().FontSize(11).FontColor(CWhite);
                    inner.Item().AlignRight()
                         .Text($"Generado: {d.GeneratedAt:dd/MM/yyyy HH:mm}")
                         .FontSize(8).FontColor("#94a3b8");
                });
            });

            // Metadata band
            col.Item().Background(CPrimary).PaddingHorizontal(14).PaddingVertical(6).Row(row =>
            {
                row.RelativeItem()
                   .Text($"Usuario: {d.UserName}")
                   .FontSize(8).FontColor(CWhite).Bold();
                row.RelativeItem().AlignCenter()
                   .Text($"Filtros: {d.Filter.FilterSummary}")
                   .FontSize(8).FontColor(CInfoBar);
                row.AutoItem().AlignRight()
                   .Text($"{d.TotalRows} registro{(d.TotalRows != 1 ? "s" : "")}")
                   .FontSize(8).FontColor(CWhite).Bold();
            });
        });
    }

    // ══ BODY ══════════════════════════════════════════════════════════════
    private static void DrawBody(IContainer c, ReportData d)
    {
        c.Column(col =>
        {
            if (d.TotalRows == 0)
            {
                col.Item().PaddingVertical(48).AlignCenter()
                   .Text("No se encontraron registros con los filtros seleccionados.")
                   .FontSize(11).FontColor(CMuted).Italic();
                return;
            }

            // ── Data table ────────────────────────────────────────────────
            col.Item().Table(table =>
            {
                if (d.Filter.IsFinanciamientos)
                    DefineFinancingColumns(table);
                else if (d.Filter.IsNotas)
                    DefineNotaColumns(table);
                else
                    DefineTransactionColumns(table);

                // Header
                table.Header(h =>
                {
                    if (d.Filter.IsFinanciamientos)
                        DrawFinancingHeader(h);
                    else if (d.Filter.IsNotas)
                        DrawNotaHeader(h);
                    else
                        DrawTransactionHeader(h);
                });

                // Rows
                bool alt = false;
                foreach (var row in d.Rows)
                {
                    var bg = alt ? CAlt : CWhite;
                    alt = !alt;

                    if (d.Filter.IsFinanciamientos)
                        DrawFinancingRow(table, row, bg);
                    else if (d.Filter.IsNotas)
                        DrawNotaRow(table, row, bg);
                    else
                        DrawTransactionRow(table, row, bg);
                }
            });

            // ── Totals ────────────────────────────────────────────────────
            DrawTotals(col, d);
        });
    }

    // ── Transaction columns ───────────────────────────────────────────────
    private static void DefineTransactionColumns(TableDescriptor t)
    {
        t.ColumnsDefinition(c =>
        {
            c.ConstantColumn(64);   // Fecha
            c.RelativeColumn(2.2f); // Comercio
            c.RelativeColumn(1.4f); // Banco / Tarjeta
            c.RelativeColumn(1.2f); // Categoría
            c.RelativeColumn(1f);   // Tipo mov.
            c.ConstantColumn(30);   // Mon.
            c.ConstantColumn(78);   // Monto
            c.ConstantColumn(52);   // Estado
        });
    }

    private static void DrawTransactionHeader(TableCellDescriptor h)
    {
        foreach (var txt in new[] { "Fecha", "Comercio", "Banco / Tarjeta",
                                    "Categoría", "Tipo", "Mon.", "Monto", "Estado" })
            HCell(h, txt, right: txt == "Monto", centered: txt is "Mon." or "Estado");
    }

    private static void DrawTransactionRow(TableDescriptor t, ReportTransactionRow r, string bg)
    {
        DCell(t, r.Date.ToString("dd/MM/yyyy"), bg);
        DCell(t, r.Merchant, bg, bold: true);
        DCell(t, BankCard(r.Bank, r.Card), bg, color: CMuted);
        DCell(t, r.Category, bg);
        DCell(t, r.MovementType, bg, color: CMuted);
        DCell(t, r.Currency, bg, color: CMuted, centered: true, fontSize: 8f);
        AmtCell(t, r.Amount, r.Currency, bg, false);
        StatusCell(t, r.Status, bg);
    }

    // ── Financing columns ─────────────────────────────────────────────────
    private static void DefineFinancingColumns(TableDescriptor t)
    {
        t.ColumnsDefinition(c =>
        {
            c.ConstantColumn(64);   // Fecha compra
            c.RelativeColumn(2f);   // Comercio / Detalle
            c.RelativeColumn(1.4f); // Banco / Tarjeta
            c.RelativeColumn(1.2f); // Tipo
            c.ConstantColumn(30);   // Mon.
            c.ConstantColumn(82);   // Cuota mensual
            c.ConstantColumn(52);   // Estado
        });
    }

    private static void DrawFinancingHeader(TableCellDescriptor h)
    {
        foreach (var (txt, right, cen) in new[]
        {
            ("Fecha",         false, false),
            ("Comercio",      false, false),
            ("Banco / Tarj.", false, false),
            ("Tipo",          false, false),
            ("Mon.",          false, true),
            ("Cuota/mes",     true,  false),
            ("Estado",        false, true),
        })
            HCell(h, txt, right: right, centered: cen);
    }

    private static void DrawFinancingRow(TableDescriptor t, ReportTransactionRow r, string bg)
    {
        DCell(t, r.Date.ToString("dd/MM/yyyy"), bg);

        // Comercio + detalle en sub-línea
        t.Cell().Background(bg).BorderBottom(0.4f).BorderColor(CBorder)
         .PaddingHorizontal(5).PaddingVertical(4).Column(col =>
         {
             col.Item().Text(r.Merchant).FontSize(8.5f).Bold();
             if (!string.IsNullOrEmpty(r.Detail))
                 col.Item().Text(r.Detail).FontSize(7f).FontColor(CMuted).Italic();
         });

        DCell(t, BankCard(r.Bank, r.Card), bg, color: CMuted);
        DCell(t, r.MovementType, bg);
        DCell(t, r.Currency, bg, color: CMuted, centered: true, fontSize: 8f);
        AmtCell(t, r.Amount, r.Currency, bg, isFinancing: true);
        StatusCell(t, r.Status, bg);
    }

    // ── Nota / Compartidos columns ────────────────────────────────────────
    private static void DefineNotaColumns(TableDescriptor t)
    {
        t.ColumnsDefinition(c =>
        {
            c.ConstantColumn(60);   // Fecha
            c.RelativeColumn(2f);   // Título / Detalle
            c.RelativeColumn(1.3f); // Persona
            c.RelativeColumn(1f);   // Categoría
            c.ConstantColumn(60);   // Dirección
            c.ConstantColumn(30);   // Mon.
            c.ConstantColumn(76);   // Total
            c.ConstantColumn(76);   // Pagado
            c.ConstantColumn(76);   // Pendiente
            c.ConstantColumn(52);   // Estado
        });
    }

    private static void DrawNotaHeader(TableCellDescriptor h)
    {
        foreach (var (txt, right) in new[]
        {
            ("Fecha",     false), ("Título",    false), ("Persona",  false),
            ("Categoría", false), ("Dirección", false), ("Mon.",     false),
            ("Total",     true),  ("Pagado",    true),  ("Pendiente",true), ("Estado", false)
        })
            HCell(h, txt, right: right, centered: txt == "Mon.");
    }

    private static void DrawNotaRow(TableDescriptor t, ReportTransactionRow r, string bg)
    {
        var dirColor = r.MovementType.Contains("cobrar") ? CGreen : "#dc2626";

        DCell(t, r.Date.ToString("dd/MM/yyyy"), bg);

        // Título + detalle en sub-línea
        t.Cell().Background(bg).BorderBottom(0.4f).BorderColor(CBorder)
         .PaddingHorizontal(5).PaddingVertical(4).Column(col =>
         {
             col.Item().Text(r.Merchant).FontSize(8.5f).Bold();
             if (!string.IsNullOrWhiteSpace(r.Detail))
                 col.Item().Text(r.Detail).FontSize(7f).FontColor(CMuted).Italic();
         });

        DCell(t, r.Person ?? "—", bg, color: string.IsNullOrWhiteSpace(r.Person) ? CMuted : CText);
        DCell(t, r.Category, bg, color: CMuted);
        DCell(t, r.MovementType, bg, color: dirColor, bold: true);
        DCell(t, r.Currency, bg, color: CMuted, centered: true, fontSize: 8f);
        AmtCell(t, r.Amount,        r.Currency, bg, isFinancing: false);
        AmtCell(t, r.PaidAmount,    r.Currency, bg, isFinancing: false, overrideColor: CGreen);
        AmtCell(t, r.PendingAmount, r.Currency, bg, isFinancing: false,
                overrideColor: r.PendingAmount > 0 ? "#dc2626" : CGreen);
        StatusCell(t, r.Status, bg);
    }

    // ── Totals block ──────────────────────────────────────────────────────
    private static void DrawTotals(ColumnDescriptor col, ReportData d)
    {
        bool hasCrc = d.TotalCrc > 0;
        bool hasUsd = d.TotalUsd > 0;
        if (!hasCrc && !hasUsd) return;

        col.Item().PaddingTop(16).Background(CHeader)
           .PaddingVertical(11).PaddingHorizontal(14).Row(row =>
           {
               row.RelativeItem()
                  .Text($"TOTAL — {d.TotalRows} registro{(d.TotalRows != 1 ? "s" : "")}")
                  .Bold().FontSize(10).FontColor(CWhite);

               var parts = new List<string>();
               if (hasCrc) parts.Add(Fmt(d.TotalCrc, "CRC"));
               if (hasUsd) parts.Add(Fmt(d.TotalUsd, "USD"));
               row.AutoItem().AlignRight()
                  .Text(string.Join("   |   ", parts))
                  .Bold().FontSize(11).FontColor(CInfoBar);
           });

        if (hasCrc && hasUsd)
        {
            col.Item().Background("#1e3a8a").PaddingVertical(5).PaddingHorizontal(14).Row(row =>
            {
                row.RelativeItem()
                   .Text("CRC")
                   .Bold().FontSize(9).FontColor(CInfoBar);
                row.RelativeItem().AlignCenter()
                   .Text(Fmt(d.TotalCrc, "CRC"))
                   .Bold().FontSize(9).FontColor(CInfoBar);
                row.RelativeItem().AlignRight()
                   .Text("USD   " + Fmt(d.TotalUsd, "USD"))
                   .Bold().FontSize(9).FontColor("#bbf7d0");
            });
        }
    }

    // ══ FOOTER ════════════════════════════════════════════════════════════
    private static void DrawFooter(IContainer c)
    {
        c.PaddingTop(6).BorderTop(0.5f).BorderColor(CBorder).Row(row =>
        {
            row.RelativeItem()
               .Text("PurchaseTracker — Documento generado automáticamente. Solo para uso personal.")
               .FontSize(7.5f).FontColor(CMuted).Italic();
            row.AutoItem().AlignRight().Text(t =>
            {
                t.Span("Página ").FontSize(7.5f).FontColor(CMuted);
                t.CurrentPageNumber().FontSize(7.5f).FontColor(CMuted);
                t.Span(" de ").FontSize(7.5f).FontColor(CMuted);
                t.TotalPages().FontSize(7.5f).FontColor(CMuted);
            });
        });
    }

    // ══ CELL HELPERS ══════════════════════════════════════════════════════

    private static void HCell(TableCellDescriptor h, string text,
        bool right = false, bool centered = false)
    {
        IContainer cell = h.Cell().Background(CHeader)
            .PaddingHorizontal(5).PaddingVertical(7);
        if (right)    cell = cell.AlignRight();
        if (centered) cell = cell.AlignCenter();
        cell.Text(text).Bold().FontSize(8.5f).FontColor(CWhite);
    }

    private static void DCell(TableDescriptor t, string text, string bg,
        bool bold = false, string? color = null, bool centered = false,
        bool right = false, float fontSize = 8.5f)
    {
        IContainer cell = t.Cell().Background(bg)
            .BorderBottom(0.4f).BorderColor(CBorder)
            .PaddingHorizontal(5).PaddingVertical(4);
        if (right)    cell = cell.AlignRight();
        if (centered) cell = cell.AlignCenter();
        var txt = cell.Text(text).FontSize(fontSize).FontColor(color ?? CText);
        if (bold) txt.Bold();
    }

    private static void AmtCell(TableDescriptor t, decimal amount, string currency,
        string bg, bool isFinancing, string? overrideColor = null)
    {
        var color = overrideColor
                 ?? (isFinancing ? CViolet
                   : currency == "USD" ? CGreen
                   : CText);
        t.Cell().Background(bg).BorderBottom(0.4f).BorderColor(CBorder)
         .PaddingHorizontal(5).PaddingVertical(4).AlignRight()
         .Text(Fmt(amount, currency)).Bold().FontSize(8.5f).FontColor(color);
    }

    private static void StatusCell(TableDescriptor t, string status, string bg)
    {
        var color = status.ToLowerInvariant() switch
        {
            "aprobada" or "activo" or "pagado"   => CGreen,
            "rechazada" or "cancelado"            => "#dc2626",
            "pendiente"                           => "#92400e",
            _                                     => CMuted
        };
        t.Cell().Background(bg).BorderBottom(0.4f).BorderColor(CBorder)
         .PaddingHorizontal(5).PaddingVertical(4).AlignCenter()
         .Text(status).FontSize(8f).FontColor(color).Bold();
    }

    private static string Fmt(decimal v, string cur) =>
        cur == "USD"
            ? $"${v.ToString("N2", En)}"
            : $"₡{v.ToString("N2", En)}";

    private static string BankCard(string bank, string card) =>
        string.IsNullOrWhiteSpace(card) ? bank : $"{bank} — {card}";
}
