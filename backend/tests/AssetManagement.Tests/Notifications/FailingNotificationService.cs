using AssetManagement.Application.Notifications;

namespace AssetManagement.Tests.Notifications;

/// <summary>
/// 专用于验证业务状态与通知写入共享事务的故障注入实现。
/// </summary>
internal sealed class FailingNotificationService : INotificationService
{
    private static InvalidOperationException Failure()
        => new("测试故障：通知写入失败");

    public Task<List<NotificationDto>> GetMyNotificationsAsync(int userId, bool unreadOnly = false)
        => throw Failure();

    public Task<int> GetUnreadCountAsync(int userId)
        => throw Failure();

    public Task MarkReadAsync(int id, int userId)
        => throw Failure();

    public Task MarkAllReadAsync(int userId)
        => throw Failure();

    public Task ClearAsync(int userId)
        => throw Failure();

    public Task<bool> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<bool>(Failure());

    public Task<int> CreateBatchAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken = default)
        => Task.FromException<int>(Failure());
}
