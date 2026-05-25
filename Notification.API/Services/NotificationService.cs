using Microsoft.Extensions.Caching.Memory;
using Notification.API.DTOs;
using Notification.API.Models;
using Notification.API.Repositories.Interfaces;
using Notification.API.Services.Interfaces;

namespace Notification.API.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string VersionKey = "notifications:list:version";
    private const string ItemPrefix = "notifications:item";

    public NotificationService(INotificationRepository repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<PagedResult<NotificationResponse>> ListForUserAsync(
        long userId, string? status, string? category, int page, int pageSize)
    {
        var v = GetVersion();
        var key = $"notifications:user:v{v}:{userId}:{status}:{category}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<NotificationResponse>? cached) && cached is not null)
            return cached;
        var (items, total) = await _repo.ListForUserAsync(userId, status, category, page, pageSize);
        var result = new PagedResult<NotificationResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(ToResponse).ToList()
        };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<PagedResult<NotificationResponse>> ListAllAsync(
        string? category, string? status, int page, int pageSize)
    {
        var v = GetVersion();
        var key = $"notifications:all:v{v}:{category}:{status}:{page}:{pageSize}";
        if (_cache.TryGetValue(key, out PagedResult<NotificationResponse>? cached) && cached is not null)
            return cached;
        var (items, total) = await _repo.ListAllAsync(category, status, page, pageSize);
        var result = new PagedResult<NotificationResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(ToResponse).ToList()
        };
        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<NotificationResponse> CreateAsync(CreateNotificationRequest req)
    {
        var notification = new NotificationRecord
        {
            UserID = req.UserID,
            Message = req.Message,
            Category = req.Category,
            Status = "Unread",
            CreatedDate = DateTime.UtcNow
        };
        var created = await _repo.AddAsync(notification);
        BumpVersion();
        return ToResponse(created);
    }

    public async Task<NotificationResponse?> MarkReadAsync(long id, long callerUserId)
    {
        var n = await _repo.GetByIdAsync(id);
        if (n is null) return null;

        // Ownership check — prevent IDOR. A user can only mark their own
        // notifications as read.
        if (n.UserID != callerUserId)
            throw new UnauthorizedAccessException(
                "You can only mark your own notifications as read.");

        if (n.Status != "Read")
        {
            n.Status = "Read";
            await _repo.UpdateAsync(n);
            Invalidate(id);
        }
        return ToResponse(n);
    }

    private int GetVersion() =>
        _cache.GetOrCreate(VersionKey, e => { e.Priority = CacheItemPriority.NeverRemove; return 0; });

    private void BumpVersion() =>
        _cache.Set(VersionKey, GetVersion() + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

    private void Invalidate(long id) { _cache.Remove($"{ItemPrefix}:{id}"); BumpVersion(); }

    private static NotificationResponse ToResponse(NotificationRecord n) =>
        new(n.NotificationID, n.UserID, n.Message, n.Category, n.Status, n.CreatedDate);
}
