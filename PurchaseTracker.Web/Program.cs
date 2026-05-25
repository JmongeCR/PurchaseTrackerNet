using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly("PurchaseTracker.Web")  // migraciones en el proyecto Web
    ));

// Auth
builder.Services.AddAuthentication("PTAuth")
    .AddCookie("PTAuth", opts =>
    {
        opts.LoginPath = "/auth/login";
        opts.AccessDeniedPath = "/auth/login";
        opts.ExpireTimeSpan = TimeSpan.FromDays(7);
        opts.SlidingExpiration = true;
        opts.Cookie.Name = "PT.Auth";
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<PurchaseTracker.Web.Services.EmailService>();
builder.Services.AddHttpContextAccessor();

// Tipo de cambio USD/CRC — GoMeta API
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<PurchaseTracker.Web.Services.ExchangeRateService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("PurchaseTracker/1.0");
});

// PWA icon generator (runs once on startup)
builder.Services.AddSingleton<PurchaseTracker.Web.Services.PwaIconService>();

// Reportes
builder.Services.AddScoped<PurchaseTracker.Web.Services.ReportService>();
builder.Services.AddSingleton<PurchaseTracker.Web.Services.PdfReportService>();
builder.Services.AddSingleton<PurchaseTracker.Web.Services.ExcelReportService>();

// QuestPDF community license
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// ╔══════════════════════════════════════════════════════════════════╗
// ║  MIGRACIONES — SOLO PARA SETUP INICIAL / DESARROLLO             ║
// ║                                                                  ║
// ║  Para aplicar migraciones en producción ejecuta manualmente:     ║
// ║    dotnet ef database update                                     ║
// ║  o desde el servidor:                                            ║
// ║    dotnet PurchaseTracker.Web.dll --migrate  (si implementas CLI)║
// ║                                                                  ║
// ║  NO activar Database.Migrate() en producción — aplícalo antes   ║
// ║  del despliegue como parte del pipeline de CI/CD.               ║
// ╚══════════════════════════════════════════════════════════════════╝
//
// [DESACTIVADO EN PRODUCCIÓN] — descomenta solo para setup/desarrollo:
//
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     db.Database.Migrate();
// }

// ── Generate PWA icons on first startup ──────────────────────────────────────
app.Services.GetRequiredService<PurchaseTracker.Web.Services.PwaIconService>()
   .EnsureIconsExist();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/auth/login"); // evitar loop: /dashboard requiere auth y puede causar redirección infinita

// Necesario en hosting detrás de proxy/IIS (siteasp.net termina SSL)
// Sin esto las cookies pueden fallar en HTTPS y generar loops de redirección
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Landing}/{action=Index}/{id?}");

app.Run();
