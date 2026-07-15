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
    /// <summary>写入单条通知；若 IdempotencyKey 已存在则静默跳过</summary>
    Task CreateAsync(CreateNotificationRequest request);
    /// <summary>批量写入通知；每条独立做幂等检查</summary>
    Task CreateBatchAsync(IEnumerable<CreateNotificationRequest> requests);
}
