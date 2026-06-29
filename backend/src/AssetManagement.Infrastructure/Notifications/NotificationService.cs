using AssetManagement.Application.Common;
using AssetManagement.Application.Notifications;
using AssetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId)
            ?? throw new BizException(4004, "通知不存在");
        n.IsRead = true;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(int userId)
    {
        await _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));
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
        catch (DbUpdateException) { } // 唯一索引冲突静默忽略（并发重复）
    }

    public async Task CreateBatchAsync(IEnumerable<CreateNotificationRequest> requests)
    {
        var requestList = requests.ToList();
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
            _db.Notifications.AddRange(toAdd);
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException) { } // 唯一索引冲突静默忽略
        }
    }

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
