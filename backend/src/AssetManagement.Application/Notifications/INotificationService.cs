namespace AssetManagement.Application.Notifications;

public class NotificationDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int FlowId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateNotificationRequest
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int FlowId { get; set; }
    public int UserId { get; set; }
    /// <summary>幂等键，相同键不重复写入；可为空（不做幂等检查）</summary>
    public string? IdempotencyKey { get; set; }
}

public interface INotificationService
{
    Task<List<NotificationDto>> GetMyNotificationsAsync(int userId, bool unreadOnly = false);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkReadAsync(int id, int userId);
    Task MarkAllReadAsync(int userId);
    Task ClearAsync(int userId);
    /// <summary>写入单条通知；返回是否实际新增，幂等键已存在时为 false。</summary>
    Task<bool> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default);
    /// <summary>批量写入通知；返回实际新增条数。</summary>
    Task<int> CreateBatchAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken cancellationToken = default);
}
