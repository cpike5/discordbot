namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing user notifications.
/// Provides methods for retrieving, creating, and managing notification lifecycle.
/// Composed of <see cref="INotificationReader"/> (queries) and <see cref="INotificationWriter"/> (mutations/purges).
/// </summary>
public interface INotificationRepository : INotificationReader, INotificationWriter
{
}
