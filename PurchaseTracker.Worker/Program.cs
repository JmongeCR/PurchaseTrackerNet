using Microsoft.EntityFrameworkCore;
using PurchaseTracker.Shared.Data;
using PurchaseTracker.Shared.Services;
using PurchaseTracker.Worker;
using PurchaseTracker.Worker.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")),
            ServiceLifetime.Scoped);

        services.AddHttpClient();

        services.AddScoped<ImapService>();
        services.AddScoped<EmlProcessorService>();
        services.AddScoped<TelegramService>();

        services.AddHostedService<ImapWorker>();
    })
    .Build();

await host.RunAsync();
