namespace PurchaseTracker.Shared.Entities;

public class Category
{
    private static readonly Dictionary<string, string> IconMap = new()
    {
        ["cart"] = "🛒", ["food"] = "🍽️", ["fuel"] = "⛽", ["health"] = "💊",
        ["entertainment"] = "🎬", ["clothes"] = "👕", ["services"] = "💡",
        ["travel"] = "✈️", ["education"] = "📚", ["home"] = "🏠",
        ["transport"] = "🚗", ["money"] = "💰", ["gaming"] = "🎮",
        ["pets"] = "🐾", ["gifts"] = "🎁", ["fitness"] = "🏋️",
        ["phone"] = "📱", ["drinks"] = "🍺", ["tools"] = "🔧",
        ["box"] = "📦", ["hospital"] = "🏥", ["graduation"] = "🎓",
        ["tacos"] = "🌮", ["coffee"] = "☕", ["shopping"] = "🛍️",
        ["computer"] = "💻", ["music"] = "🎵", ["beach"] = "🏖️",
        ["bus"] = "🚌", ["tag"] = "🏷️"
    };

    public static IReadOnlyDictionary<string, string> AllIcons => IconMap;

    public int CategoryId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366f1";
    public string Icon { get; set; } = "tag";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public string IconEmoji => IconMap.TryGetValue(Icon, out var e) ? e : "🏷️";
}
