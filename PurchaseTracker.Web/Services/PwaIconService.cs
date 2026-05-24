using IOPath = System.IO.Path;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PurchaseTracker.Web.Services;

/// <summary>
/// Generates PWA PNG icons on startup (only if they don't already exist).
/// Design: dark-indigo background · white credit-card silhouette · gold chip · NFC arcs
/// </summary>
public class PwaIconService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PwaIconService> _logger;

    public PwaIconService(IWebHostEnvironment env, ILogger<PwaIconService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public void EnsureIconsExist()
    {
        var iconsDir = IOPath.Combine(_env.WebRootPath, "icons");
        Directory.CreateDirectory(iconsDir);

        GenerateIfMissing(IOPath.Combine(iconsDir, "icon-192.png"), 192);
        GenerateIfMissing(IOPath.Combine(iconsDir, "icon-512.png"), 512);
    }

    private void GenerateIfMissing(string path, int size)
    {
        if (File.Exists(path)) return;

        try
        {
            GenerateIcon(path, size);
            _logger.LogInformation("PWA icon generated: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PWA icon: {Path}", path);
        }
    }

    /// <summary>Builds a rounded-rectangle path.</summary>
    private static IPath RoundedRect(float x, float y, float w, float h, float r)
    {
        r = MathF.Min(r, MathF.Min(w, h) / 2f);
        var pb = new PathBuilder();
        pb.MoveTo(new PointF(x + r, y));
        pb.LineTo(new PointF(x + w - r, y));
        pb.ArcTo(r, r, 0, false, true, new PointF(x + w, y + r));
        pb.LineTo(new PointF(x + w, y + h - r));
        pb.ArcTo(r, r, 0, false, true, new PointF(x + w - r, y + h));
        pb.LineTo(new PointF(x + r, y + h));
        pb.ArcTo(r, r, 0, false, true, new PointF(x, y + h - r));
        pb.LineTo(new PointF(x, y + r));
        pb.ArcTo(r, r, 0, false, true, new PointF(x + r, y));
        pb.CloseFigure();
        return pb.Build();
    }

    private static void GenerateIcon(string path, int size)
    {
        float s = size / 192f;

        // ── Colour palette ────────────────────────────────────────────────
        var bgColor   = Color.FromRgb(0x1e, 0x3a, 0x8a);          // indigo-800
        var bgOverlay = Color.FromRgba(0x0f, 0x17, 0x2a, 160);    // dark slate overlay
        var cardColor = Color.FromRgba(255, 255, 255, 235);         // near-white card
        var barColor  = Color.FromRgb(0x1e, 0x40, 0xaf);           // indigo-700
        var chipGold  = Color.FromRgb(0xd4, 0xa8, 0x17);           // gold
        var chipDark  = Color.FromRgb(0xa0, 0x7c, 0x00);           // dark gold
        var nfcColor  = Color.FromRgba(0x1e, 0x40, 0xaf, 190);    // indigo, semi

        using var img = new Image<Rgba32>(size, size);

        img.Mutate(ctx =>
        {
            // ── 1. Background ─────────────────────────────────────────────
            ctx.Fill(bgColor);
            ctx.Fill(bgOverlay, new RectangularPolygon(0, size * 0.5f, size, size * 0.5f));

            // ── 2. Subtle inset rounded square ────────────────────────────
            float pad = 8 * s;
            ctx.Fill(Color.FromRgba(255, 255, 255, 10),
                RoundedRect(pad, pad, size - pad * 2, size - pad * 2, 20 * s));

            // ── 3. Card body (white rounded rect) ─────────────────────────
            float cw = 140 * s, ch = 88 * s;
            float cx = (size - cw) / 2f, cy = (size - ch) / 2f;
            ctx.Fill(cardColor, RoundedRect(cx, cy, cw, ch, 10 * s));

            // ── 4. Top accent bar ─────────────────────────────────────────
            float barH = 20 * s;
            // Full bar with rounded top corners only
            ctx.Fill(barColor, RoundedRect(cx, cy, cw, barH, 10 * s));
            // Square off the lower half of the bar so it meets the card body cleanly
            ctx.Fill(barColor, new RectangularPolygon(cx, cy + barH * 0.5f, cw, barH * 0.5f));

            // ── 5. EMV Chip ───────────────────────────────────────────────
            float chpX = cx + 14 * s;
            float chpY = cy + barH + 10 * s;
            float chpW = 26 * s, chpH = 20 * s;
            ctx.Fill(chipGold, RoundedRect(chpX, chpY, chpW, chpH, 3 * s));

            float lineW = MathF.Max(1f, s);
            float chpMidX = chpX + chpW / 2f;
            float chpMidY = chpY + chpH / 2f;
            ctx.DrawLine(chipDark, lineW,
                new PointF(chpMidX, chpY + 2 * s),
                new PointF(chpMidX, chpY + chpH - 2 * s));
            ctx.DrawLine(chipDark, lineW,
                new PointF(chpX + 2 * s, chpMidY),
                new PointF(chpX + chpW - 2 * s, chpMidY));

            // ── 6. NFC arcs ───────────────────────────────────────────────
            float arcW  = MathF.Max(1f, 1.5f * s);
            float nfcX  = chpX + chpW + 8 * s;
            float nfcY  = chpMidY;
            DrawArc(ctx, nfcX,           nfcY,  6 * s, nfcColor, arcW);
            DrawArc(ctx, nfcX +  7 * s,  nfcY, 11 * s, nfcColor, arcW);
            DrawArc(ctx, nfcX + 14 * s,  nfcY, 16 * s, nfcColor, arcW);

            // ── 7. "PT" lettering ─────────────────────────────────────────
            float lw  = MathF.Max(1.5f, 2f * s);
            float ltX = cx + 14 * s;
            float ltY = cy + ch - 22 * s;

            // P: vertical stroke + top bar + mid bar + right side (top half)
            ctx.DrawLine(nfcColor, lw,
                new PointF(ltX,           ltY),
                new PointF(ltX,           ltY + 11 * s));
            ctx.DrawLine(nfcColor, lw,
                new PointF(ltX,           ltY),
                new PointF(ltX + 7 * s,   ltY));
            ctx.DrawLine(nfcColor, lw,
                new PointF(ltX,           ltY + 5.5f * s),
                new PointF(ltX + 7 * s,   ltY + 5.5f * s));
            ctx.DrawLine(nfcColor, lw,
                new PointF(ltX + 7 * s,   ltY),
                new PointF(ltX + 7 * s,   ltY + 5.5f * s));

            // T: horizontal bar + vertical stroke
            float tX = ltX + 13 * s;
            ctx.DrawLine(nfcColor, lw,
                new PointF(tX,             ltY),
                new PointF(tX + 10 * s,    ltY));
            ctx.DrawLine(nfcColor, lw,
                new PointF(tX + 5 * s,     ltY),
                new PointF(tX + 5 * s,     ltY + 11 * s));
        });

        img.SaveAsPng(path);
    }

    private static void DrawArc(IImageProcessingContext ctx,
        float cx, float midY, float radius, Color color, float thickness)
    {
        const int segments = 24;
        float startAngle = -MathF.PI / 2.6f;
        float endAngle   =  MathF.PI / 2.6f;
        var pts = new PointF[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = startAngle + (endAngle - startAngle) * i / segments;
            pts[i] = new PointF(cx + MathF.Cos(t) * radius,
                                midY + MathF.Sin(t) * radius);
        }
        ctx.DrawLine(color, thickness, pts);
    }
}
