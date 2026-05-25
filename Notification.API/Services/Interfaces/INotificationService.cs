using Notification.API.DTOs;

namespace Notification.API.Services.Interfaces;

public interface INotificationService
{
    Task<PagedResult<NotificationResponse>> ListForUserAsync(
        long userId, string? status, string? category, int page, int pageSize);
    Task<PagedResult<NotificationResponse>> ListAllAsync(
        string? category, string? status, int page, int pageSize);
    Task<NotificationResponse> CreateAsync(CreateNotificationRequest req);

    /// <summary>
    /// Mark a notification as read. Returns null if not found, throws
    /// UnauthorizedAccessException if the notification does not belong to the caller.
    /// </summary>
    Task<NotificationResponse?> MarkReadAsync(long id, long callerUserId);
}
