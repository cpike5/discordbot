namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing user notifications.
/// Provides methods for creating, retrieving, and managing notification lifecycle.
/// Composed of <see cref="INotificationSender"/> (creation) and <see cref="INotificationManager"/> (query/lifecycle).
/// </summary>
public interface INotificationService : INotificationSender, INotificationManager
{
}
