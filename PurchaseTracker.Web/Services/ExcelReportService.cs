using ClosedXML.Excel;
using System.Globalization;
using PurchaseTracker.Web.ViewModels;

namespace PurchaseTracker.Web.Services;

public class ExcelReportService
{
    public byte[] Generate(ReportData data)
    {
        using var wb = new XLWorkbook();
        var ws  = wb.Worksheets.Add("Reporte");
        int row = 1;

        bool isFin  = data.Filter.IsFinanciamientos;
        bool isNota = data.Filter.IsNotas;
        int  maxCol = isNota ? 10 : 8;

        // ── Encabezado ────────────────────────────────────────────────────
        row = WriteMetaBlock(ws, row, data);

        // ── Encabezado de columnas ────────────────────────────────────────
        row = WriteColumnHeaders(ws, row, isFin, isNota);

        // ── Filas de datos ────────────────────────────────────────────────
        bool alt = false;

        if (data.TotalRows == 0)
        {
            ws.Cell(row, 1).Value = "No se encontraron registros con los filtros seleccionados.";
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#64748b");
            ws.Range(row, 1, row, maxCol).Merge();
            row++;
        }
        else
        {
            foreach (var item in data.Rows)
            {
                row = isNota
                    ? WriteNotaRow(ws, row, item, alt)
                    : WriteDataRow(ws, row, item, alt, isFin);
                alt = !alt;
            }
        }

        // ── Totales ───────────────────────────────────────────────────────
        if (data.TotalRows > 0)
            row = WriteTotals(ws, row, data, isFin, isNota);

        // ── Anchos de columna ─────────────────────────────────────────────
        ApplyColumnWidths(ws, isFin, isNota);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Meta block ────────────────────────────────────────────────────────
    private static int WriteMetaBlock(IXLWorksheet ws, int row, ReportData d)
    {
        int cols = d.Filter.IsFinanciamientos ? 7 : d.Filter.IsNotas ? 10 : 8;

        SetBig(ws, row++, cols, "PurchaseTracker — Reporte financiero personal", "#1e40af", 14);
        SetBig(ws, row++, cols, d.Filter.SourceLabel, "#1e40af", 12);
        SetMeta(ws, row++, cols, $"Generado: {d.GeneratedAt:dd/MM/yyyy HH:mm}   ·   Usuario: {d.UserName}");
        SetMeta(ws, row++, cols, $"Filtros: {d.Filter.FilterSummary}");
        SetMeta(ws, row++, cols, $"Total: {d.TotalRows} registro{(d.TotalRows != 1 ? "s" : "")}");

        row++; // blank
        return row;
    }

    // ── Column headers ────────────────────────────────────────────────────
    private static int WriteColumnHeaders(IXLWorksheet ws, int row, bool isFin, bool isNota)
    {
        string[] headers = isNota
            ? ["Fecha", "Título", "Detalle", "Persona", "Categoría", "Dirección",
               "Moneda", "Total", "Pagado", "Pendiente", "Estado"]
            : isFin
            ? ["Fecha", "Comercio", "Detalle", "Banco / Tarjeta", "Tipo", "Moneda", "Cuota/mes", "Estado"]
            : ["Fecha", "Comercio", "Banco / Tarjeta", "Categoría", "Tipo mov.", "Moneda", "Monto", "Estado"];

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0f172a");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#1e40af");
        }
        return row + 1;
    }

    // ── Data row ──────────────────────────────────────────────────────────
    private static int WriteDataRow(IXLWorksheet ws, int row,
        ReportTransactionRow item, bool alt, bool isFin)
    {
        var fill     = alt ? XLColor.FromHtml("#f8fafc") : XLColor.White;
        var amtColor = item.IsFinancing ? XLColor.FromHtml("#7c3aed")
                     : item.Currency == "USD" ? XLColor.FromHtml("#16a34a")
                     : XLColor.FromHtml("#0f172a");

        ws.Cell(row, 1).Value = item.Date.Date;
        ws.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";

        ws.Cell(row, 2).Value = item.Merchant;
        ws.Cell(row, 2).Style.Font.Bold = true;

        if (isFin)
        {
            ws.Cell(row, 3).Value = item.Detail ?? "";
            ws.Cell(row, 3).Style.Font.Italic    = true;
            ws.Cell(row, 3).Style.Font.FontColor = XLColor.FromHtml("#64748b");
            ws.Cell(row, 4).Value = BankCard(item.Bank, item.Card);
            ws.Cell(row, 5).Value = item.MovementType;
            ws.Cell(row, 6).Value = item.Currency;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 7).Value = item.Amount;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Cell(row, 7).Style.Font.FontColor = amtColor;
            ws.Cell(row, 8).Value = item.Status;
            ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        else
        {
            ws.Cell(row, 3).Value = BankCard(item.Bank, item.Card);
            ws.Cell(row, 4).Value = item.Category;
            ws.Cell(row, 5).Value = item.MovementType;
            ws.Cell(row, 6).Value = item.Currency;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 7).Value = item.Amount;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Cell(row, 7).Style.Font.FontColor = amtColor;
            ws.Cell(row, 8).Value = item.Status;
            ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Status color
        ws.Cell(row, 8).Style.Font.FontColor = item.Status.ToLowerInvariant() switch
        {
            "aprobada" or "activo" or "pagado"  => XLColor.FromHtml("#16a34a"),
            "rechazada" or "cancelado"           => XLColor.FromHtml("#dc2626"),
            "pendiente"                          => XLColor.FromHtml("#92400e"),
            _                                    => XLColor.FromHtml("#64748b")
        };

        // Row styling
        ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = fill;
        ws.Range(row, 1, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        ws.Range(row, 1, row, 8).Style.Border.BottomBorderColor = XLColor.FromHtml("#e2e8f0");

        return row + 1;
    }

    // ── Nota row ──────────────────────────────────────────────────────────
    private static int WriteNotaRow(IXLWorksheet ws, int row, ReportTransactionRow item, bool alt)
    {
        var fill = alt ? XLColor.FromHtml("#f8fafc") : XLColor.White;
        var dirColor = item.MovementType.Contains("cobrar")
            ? XLColor.FromHtml("#16a34a") : XLColor.FromHtml("#dc2626");

        ws.Cell(row, 1).Value = item.Date.Date;
        ws.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Cell(row, 2).Value = item.Merchant;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = item.Detail ?? "";
        ws.Cell(row, 3).Style.Font.Italic = true;
        ws.Cell(row, 3).Style.Font.FontColor = XLColor.FromHtml("#64748b");
        ws.Cell(row, 4).Value = item.Person ?? "—";
        ws.Cell(row, 5).Value = item.Category;
        ws.Cell(row, 6).Value = item.MovementType;
        ws.Cell(row, 6).Style.Font.FontColor = dirColor;
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = item.Currency;
        ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Total
        ws.Cell(row, 8).Value = item.Amount;
        ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 8).Style.Font.Bold = true;
        ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // Pagado
        ws.Cell(row, 9).Value = item.PaidAmount;
        ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 9).Style.Font.FontColor = XLColor.FromHtml("#16a34a");
        ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // Pendiente
        ws.Cell(row, 10).Value = item.PendingAmount;
        ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 10).Style.Font.FontColor = item.PendingAmount > 0
            ? XLColor.FromHtml("#dc2626") : XLColor.FromHtml("#16a34a");
        ws.Cell(row, 10).Style.Font.Bold = true;
        ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // Estado
        ws.Cell(row, 11).Value = item.Status;
        ws.Cell(row, 11).Style.Font.FontColor = item.Status switch
        {
            "pagado"    => XLColor.FromHtml("#16a34a"),
            "parcial"   => XLColor.FromHtml("#92400e"),
            "pendiente" => XLColor.FromHtml("#dc2626"),
            _           => XLColor.FromHtml("#64748b")
        };
        ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range(row, 1, row, 11).Style.Fill.BackgroundColor = fill;
        ws.Range(row, 1, row, 11).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        ws.Range(row, 1, row, 11).Style.Border.BottomBorderColor = XLColor.FromHtml("#e2e8f0");

        return row + 1;
    }

    // ── Totals ────────────────────────────────────────────────────────────
    private static int WriteTotals(IXLWorksheet ws, int row, ReportData d, bool isFin, bool isNota)
    {
        row++; // blank line before totals

        int amtCol   = isNota ? 8 : 7;
        int curCol   = isNota ? 7 : 6;
        int labelEnd = isNota ? 6 : 5;

        void TotalRow(string label, string currency, decimal amount, string fillHex, string textHex)
        {
            ws.Range(row, 1, row, labelEnd).Merge();
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 10;

            ws.Cell(row, curCol).Value = currency;
            ws.Cell(row, amtCol).Value = amount;
            ws.Cell(row, amtCol).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, amtCol).Style.Font.Bold = true;
            ws.Cell(row, amtCol).Style.Font.FontSize = 10;

            ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromHtml(fillHex);
            ws.Range(row, 1, row, 8).Style.Font.FontColor       = XLColor.FromHtml(textHex);
            row++;
        }

        bool hasCrc = d.TotalCrc > 0;
        bool hasUsd = d.TotalUsd > 0;

        if (hasCrc)
            TotalRow($"TOTAL — {d.TotalRows} registro{(d.TotalRows != 1 ? "s" : "")} (CRC)",
                     "CRC", d.TotalCrc, "#0f172a", "#bfdbfe");
        if (hasUsd)
            TotalRow(hasCrc
                     ? $"TOTAL (USD)"
                     : $"TOTAL — {d.TotalRows} registro{(d.TotalRows != 1 ? "s" : "")} (USD)",
                     "USD", d.TotalUsd, "#1e3a8a", "#bbf7d0");

        return row;
    }

    // ── Column widths ─────────────────────────────────────────────────────
    private static void ApplyColumnWidths(IXLWorksheet ws, bool isFin, bool isNota)
    {
        if (isNota)
        {
            ws.Column(1).Width  = 12;  // Fecha
            ws.Column(2).Width  = 26;  // Título
            ws.Column(3).Width  = 34;  // Detalle
            ws.Column(4).Width  = 20;  // Persona
            ws.Column(5).Width  = 18;  // Categoría
            ws.Column(6).Width  = 13;  // Dirección
            ws.Column(7).Width  = 9;   // Moneda
            ws.Column(8).Width  = 13;  // Total
            ws.Column(9).Width  = 13;  // Pagado
            ws.Column(10).Width = 13;  // Pendiente
            ws.Column(11).Width = 13;  // Estado
        }
        else
        {
            ws.Column(1).Width = 12;
            ws.Column(2).Width = 30;
            ws.Column(3).Width = isFin ? 38 : 22;
            ws.Column(4).Width = isFin ? 22 : 20;
            ws.Column(5).Width = 18;
            ws.Column(6).Width = 9;
            ws.Column(7).Width = 14;
            ws.Column(8).Width = 13;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SetBig(IXLWorksheet ws, int row, int cols, string text, string hex, double size)
    {
        ws.Cell(row, 1).Value = text;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = size;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(hex);
        ws.Range(row, 1, row, cols).Merge();
    }

    private static void SetMeta(IXLWorksheet ws, int row, int cols, string text)
    {
        ws.Cell(row, 1).Value = text;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#64748b");
        ws.Cell(row, 1).Style.Font.FontSize  = 10;
        ws.Range(row, 1, row, cols).Merge();
    }

    private static string BankCard(string bank, string card) =>
        string.IsNullOrWhiteSpace(card) ? bank : $"{bank} — {card}";
}
