using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace PurchaseTracker.Web.Services;

/// <summary>
/// Obtiene el tipo de cambio USD/CRC desde la API de GoMeta y lo cachea
/// para no saturar el servicio externo.
/// </summary>
public class ExchangeRateService
{
    private readonly HttpClient     _http;
    private readonly IMemoryCache   _cache;
    private readonly ILogger<ExchangeRateService> _log;

    private const string ApiUrl   = "https://apis.gometa.org/tdc/tdc.json";
    private const string CacheKey = "gometa_tdc";

    public ExchangeRateService(
        HttpClient http,
        IMemoryCache cache,
        ILogger<ExchangeRateService> log)
    {
        _http  = http;
        _cache = cache;
        _log   = log;
    }

    /// <summary>
    /// Retorna los datos de tipo de cambio.
    /// Usa caché de 30 minutos en caso de éxito, 5 minutos en caso de error.
    /// Nunca lanza excepción — devuelve ExchangeRateData.IsError = true si falla.
    /// </summary>
    public async Task<ExchangeRateData> GetRatesAsync()
    {
        if (_cache.TryGetValue(CacheKey, out ExchangeRateData? cached) && cached is not null)
            return cached;

        try
        {
            using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var json       = await _http.GetStringAsync(ApiUrl, cts.Token);
            var doc        = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            var data = new ExchangeRateData
            {
                Compra    = GetDecimal(root, "compra"),
                Venta     = GetDecimal(root, "venta"),
                CompraDate = GetString(root, "compra_date"),
                VentaDate  = GetString(root, "venta_date"),
                Updated   = GetString(root, "updated"),
                IsError   = false,
                FetchedAt = DateTime.UtcNow
            };

            // Datos sanos → cache 30 min
            _cache.Set(CacheKey, data, TimeSpan.FromMinutes(30));
            return data;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo obtener el tipo de cambio de GoMeta");

            // Error → cache 5 min para no martillar la API
            var error = ExchangeRateData.Empty;
            _cache.Set(CacheKey, error, TimeSpan.FromMinutes(5));
            return error;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static decimal GetDecimal(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var el)) return 0m;
        if (el.ValueKind == JsonValueKind.Number)  return el.GetDecimal();
        if (el.ValueKind == JsonValueKind.String &&
            decimal.TryParse(el.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return 0m;
    }

    private static string? GetString(JsonElement root, string prop)
        => root.TryGetProperty(prop, out var el) ? el.GetString() : null;
}
