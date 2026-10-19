using DynamicIsland.Core;

namespace DynamicIsland.Models;

public sealed class NotificationRule
{
    public string Match { get; set; } = string.Empty;
    public NotificationBehavior Behavior { get; set; } = NotificationBehavior.Standard;
    public string AccentColor { get; set; } = string.Empty;
}
