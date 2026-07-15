using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace AssetManagement.Infrastructure.Notifications;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public async Task<List<NotificationDto>> GetMyNotificationsAsync(int userId, bool unreadOnly = false)
    {
        var q = _db.Notifications.Where(x => x.UserId == userId);
        if (unreadOnly) q = q.Where(x => !x.IsRead);
        var list = await q.OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
        => await _db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead);

    public async Task MarkReadAsync(int id, int userId)
    {
        var affected = await _db.Notifications
            .Where(x => x.Id == id && x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));
        if (affected == 0) throw new BizException(4004, "通知不存在");
    }

    public async Task MarkAllReadAsync(int userId)
    {
        await _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));
    }

    public async Task ClearAsync(int userId)
    {
        await _db.Notifications
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync();
    }

    public async Task CreateAsync(CreateNotificationRequest request)
    {
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            if (await _db.Notifications.AnyAsync(n => n.IdempotencyKey == request.IdempotencyKey))
                return;
        }
        _db.Notifications.Add(ToEntity(request));
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            DetachAddedNotifications();
        } // 唯一索引冲突静默忽略（并发重复）
    }

    public async Task CreateBatchAsync(IEnumerable<CreateNotificationRequest> requests)
    {
        var requestList = DeduplicateInMemory(requests);
        var keys = requestList
            .Where(r => !string.IsNullOrEmpty(r.IdempotencyKey))
            .Select(r => r.IdempotencyKey!)
            .ToList();

        var existingKeys = keys.Count > 0
            ? (await _db.Notifications
                .Where(n => n.IdempotencyKey != null && keys.Contains(n.IdempotencyKey))
                .Select(n => n.IdempotencyKey!)
                .ToListAsync())
                .ToHashSet()
            : new HashSet<string>();

        var toAdd = requestList
            .Where(r => string.IsNullOrEmpty(r.IdempotencyKey) || !existingKeys.Contains(r.IdempotencyKey))
            .Select(ToEntity)
            .ToList();

        if (toAdd.Count > 0)
        {
            await SaveBatchIgnoringDuplicateKeysAsync(toAdd);
        }
    }

    private async Task SaveBatchIgnoringDuplicateKeysAsync(List<Domain.Entities.Notification> notifications)
    {
        _db.Notifications.AddRange(notifications);
        try
        {
            await _db.SaveChangesAsync();
            return;
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // 批量插入遇到并发重复 key 时，不能丢掉同批其他通知。
            DetachAddedNotifications();
        }

        var remainingKeys = notifications
            .Where(n => !string.IsNullOrEmpty(n.IdempotencyKey))
            .Select(n => n.IdempotencyKey!)
            .ToArray();
        var existingKeys = remainingKeys.Length > 0
            ? (await _db.Notifications
                .Where(n => n.IdempotencyKey != null && remainingKeys.Contains(n.IdempotencyKey))
                .Select(n => n.IdempotencyKey!)
                .ToListAsync())
                .ToHashSet()
            : new HashSet<string>();

        var remaining = notifications
            .Where(n => string.IsNullOrEmpty(n.IdempotencyKey) || !existingKeys.Contains(n.IdempotencyKey))
            .ToList();

        if (remaining.Count == 0) return;

        _db.Notifications.AddRange(remaining);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            DetachAddedNotifications();
            await SaveIndividuallyIgnoringDuplicateKeysAsync(remaining);
        }
    }

    private async Task SaveIndividuallyIgnoringDuplicateKeysAsync(IEnumerable<Domain.Entities.Notification> notifications)
    {
        foreach (var notification in notifications)
        {
            if (!string.IsNullOrEmpty(notification.IdempotencyKey) &&
                await _db.Notifications.AnyAsync(n => n.IdempotencyKey == notification.IdempotencyKey))
            {
                continue;
            }

            _db.Notifications.Add(notification);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                DetachAddedNotifications();
            }
        }
    }

    private static List<CreateNotificationRequest> DeduplicateInMemory(IEnumerable<CreateNotificationRequest> requests)
    {
        var result = new List<CreateNotificationRequest>();
        var seenKeys = new HashSet<string>();
        foreach (var request in requests)
        {
            if (string.IsNullOrEmpty(request.IdempotencyKey))
            {
                result.Add(request);
                continue;
            }

            if (seenKeys.Add(request.IdempotencyKey))
            {
                result.Add(request);
            }
        }

        return result;
    }

    private void DetachAddedNotifications()
    {
        foreach (var entry in _db.ChangeTracker.Entries<Domain.Entities.Notification>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is MySqlException { Number: 1062 };

    private static Domain.Entities.Notification ToEntity(CreateNotificationRequest r) => new()
    {
        Type = r.Type,
        Title = r.Title,
        Body = r.Body,
        FlowId = r.FlowId,
        UserId = r.UserId,
        IdempotencyKey = r.IdempotencyKey,
        CreatedAt = DateTime.UtcNow,
    };

    private static NotificationDto ToDto(Domain.Entities.Notification n) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Title = n.Title,
        Body = n.Body,
        FlowId = n.FlowId,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt,
    };
}
