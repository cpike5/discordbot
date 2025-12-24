// src/DiscordBot.Bot/ViewModels/Components/SkeletonViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

public record SkeletonViewModel
{
    public SkeletonType Type { get; init; } = SkeletonType.Text;
    public string? Width { get; init; }      // Tailwind class like "w-3/4" or "w-full"
    public string? Height { get; init; }     // Tailwind class like "h-4" or "h-6"
    public bool Rounded { get; init; } = true;
    public bool Animate { get; init; } = true;
    public string? CssClass { get; init; }   // Additional classes
}

public enum SkeletonType
{
    Text,           // Single line text (h-4 default)
    Title,          // Heading text (h-6 default)
    Avatar,         // Circle for avatars (w-10 h-10 rounded-full)
    AvatarSmall,    // Small avatar (w-8 h-8)
    AvatarLarge,    // Large avatar (w-16 h-16)
    Button,         // Button shape (h-10 w-24)
    Card,           // Full card rectangle
    Rectangle       // Custom rectangle
}
