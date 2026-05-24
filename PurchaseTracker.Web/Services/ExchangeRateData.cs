namespace PurchaseTracker.Web.Services;

/// <summary>
/// DTO que mapea la respuesta de https://apis.gometa.org/tdc/tdc.json
/// </summary>
public class ExchangeRateData
{
    public decimal Compra    { get; init; }
    public decimal Venta     { get; init; }
    public string? CompraDate { get; init; }
    public string? VentaDate  { get; init; }
    public string? Updated   { get; init; }

    /// <summary>true cuando la API no respondió o falló el parse</summary>
    public bool IsError { get; init; }

    /// <summary>Instante en que se obtuvo el dato (o se registró el error)</summary>
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;

    public static ExchangeRateData Empty => new() { IsError = true, FetchedAt = DateTime.UtcNow };
}
