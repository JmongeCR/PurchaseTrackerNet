using PurchaseTracker.Shared.Entities;

namespace PurchaseTracker.Web.ViewModels;

// ── Filter (lo que viene del formulario) ──────────────────────────────────────

public class ReportFilterViewModel
{
    /// <summary>"movimientos" | "financiamientos" | "notas"</summary>
    public string ReportSource { get; set; } = "movimientos";

    // ── Rango de fechas ───────────────────────────────────────────────────
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate   { get; set; }
    public int?      Month     { get; set; }
    public int?      Year      { get; set; }

    // ── Filtros comunes ───────────────────────────────────────────────────
    public string? Bank       { get; set; }
    public string? Currency   { get; set; }
    public int?    CardId     { get; set; }
    public string? Status     { get; set; }

    // ── Solo movimientos ──────────────────────────────────────────────────
    public int?    CategoryId   { get; set; }
    public string? MovementType { get; set; }
    public string? CardType     { get; set; }

    // ── Solo notas ────────────────────────────────────────────────────────
    public string? Person    { get; set; }
    public string? Direction { get; set; }  // "cobrar" | "pagar"

    // ── Dropdowns (solo para la vista) ────────────────────────────────────
    public List<Card>     Cards      { get; set; } = new();
    public List<Category> Categories { get; set; } = new();

    // ── Helpers ───────────────────────────────────────────────────────────
    public bool IsMovimientos     => ReportSource == "movimientos";
    public bool IsFinanciamientos => ReportSource == "financiamientos";
    public bool IsNotas           => ReportSource == "notas";

    public string SourceLabel => ReportSource switch
    {
        "financiamientos" => "Financiamientos",
        "notas"           => "Notas / Compartidos",
        _                 => "Movimientos"
    };

    public string FilterSummary
    {
        get
        {
            var p = new List<string>();
            if (Month.HasValue && Year.HasValue)
                p.Add($"{CrMonth(Month.Value)} {Year}");
            else
            {
                if (StartDate.HasValue) p.Add($"Desde {StartDate:dd/MM/yyyy}");
                if (EndDate.HasValue)   p.Add($"Hasta {EndDate:dd/MM/yyyy}");
            }
            if (!string.IsNullOrWhiteSpace(Bank))         p.Add($"Banco: {Bank}");
            if (!string.IsNullOrWhiteSpace(Currency))     p.Add($"Moneda: {Currency}");
            if (!string.IsNullOrWhiteSpace(MovementType)) p.Add($"Tipo: {MovementType}");
            if (!string.IsNullOrWhiteSpace(CardType))     p.Add(CardType == "credito" ? "Crédito" : "Débito");
            if (!string.IsNullOrWhiteSpace(Person))       p.Add($"Persona: {Person}");
            if (!string.IsNullOrWhiteSpace(Direction))    p.Add(Direction == "cobrar" ? "Por cobrar" : "Por pagar");
            if (!string.IsNullOrWhiteSpace(Status))       p.Add($"Estado: {Status}");
            return p.Count > 0 ? string.Join(" · ", p) : "Todos los registros";
        }
    }

    private static string CrMonth(int m) =>
        System.Globalization.CultureInfo.GetCultureInfo("es-CR")
              .DateTimeFormat.GetMonthName(m);
}

// ── Resultado del reporte ─────────────────────────────────────────────────────

public class ReportData
{
    public ReportFilterViewModel    Filter      { get; set; } = new();
    public List<ReportTransactionRow> Rows      { get; set; } = new();
    public decimal                  TotalCrc    { get; set; }
    public decimal                  TotalUsd    { get; set; }
    public DateTime                 GeneratedAt { get; set; } = DateTime.Now;
    public string                   UserName    { get; set; } = "";

    public int TotalRows => Rows.Count;
}

// ── Fila unificada ────────────────────────────────────────────────────────────

public class ReportTransactionRow
{
    /// <summary>"transaccion" | "financiamiento"</summary>
    public string   Source       { get; set; } = "transaccion";
    public int      SourceId     { get; set; }
    public DateTime Date         { get; set; }
    public string   Bank         { get; set; } = "";
    public string   Card         { get; set; } = "";
    public string   Merchant     { get; set; } = "";
    public string   Category     { get; set; } = "";
    public string   MovementType { get; set; } = "";
    public string   Currency     { get; set; } = "";
    public decimal  Amount       { get; set; }
    public string   Status        { get; set; } = "";
    /// <summary>Financiamientos: detalle de cuotas. Notas: descripción/notas.</summary>
    public string?  Detail        { get; set; }
    /// <summary>Solo notas: persona relacionada</summary>
    public string?  Person        { get; set; }
    /// <summary>Solo notas: monto pagado</summary>
    public decimal  PaidAmount    { get; set; }
    /// <summary>Solo notas: saldo pendiente</summary>
    public decimal  PendingAmount { get; set; }

    public bool IsFinancing => Source == "financiamiento";
    public bool IsNota      => Source == "nota";
}
