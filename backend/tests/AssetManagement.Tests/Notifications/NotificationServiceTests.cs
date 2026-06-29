using AssetManagement.Application.Notifications;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Tests.Notifications;

public class NotificationServiceTests : MySqlFixtureBase
{
    [Fact]
    public async Task CreateBatch_keeps_other_notifications_when_same_batch_contains_duplicate_idempotency_key()
    {
        var service = new NotificationService(_db);

        await service.CreateBatchAsync(new[]
        {
            NewRequest("dup-key", userId: 1, title: "第一次"),
            NewRequest("dup-key", userId: 1, title: "重复项"),
            NewRequest("unique-key", userId: 2, title: "独立通知"),
        });

        var notifications = await _db.Notifications
            .OrderBy(x => x.Id)
            .Select(x => new { x.IdempotencyKey, x.Title, x.UserId })
            .ToListAsync();

        notifications.Should().HaveCount(2);
        notifications.Select(x => x.IdempotencyKey)
            .Should().BeEquivalentTo("dup-key", "unique-key");
        notifications.Should().Contain(x => x.Title == "独立通知" && x.UserId == 2);
    }

    [Fact]
    public async Task Create_keeps_context_usable_after_duplicate_idempotency_key()
    {
        var service = new NotificationService(_db);

        await service.CreateAsync(NewRequest("dup-key", userId: 1, title: "第一次"));

        // 模拟并发重复键导致 SaveChanges 失败时，ChangeTracker 中已有失败的 Added 实体。
        _db.Notifications.Add(new Domain.Entities.Notification
        {
            Type = "approval_pending",
            Title = "重复插入",
            Body = "模拟并发重复键",
            FlowId = 101,
            UserId = 2,
            IdempotencyKey = "dup-key",
            CreatedAt = DateTime.UtcNow,
        });

        await service.CreateAsync(NewRequest("unique-key", userId: 2, title: "触发冲突的同批通知"));
        await service.CreateAsync(NewRequest("next-key", userId: 3, title: "后续通知"));

        var notifications = await _db.Notifications
            .OrderBy(x => x.Id)
            .Select(x => new { x.IdempotencyKey, x.Title, x.UserId })
            .ToListAsync();

        notifications.Should().HaveCount(2);
        notifications.Should().Contain(x => x.IdempotencyKey == "dup-key" && x.UserId == 1);
        notifications.Should().Contain(x => x.IdempotencyKey == "next-key" && x.UserId == 3);
    }

    private static CreateNotificationRequest NewRequest(string key, int userId, string title) => new()
    {
        Type = "approval_pending",
        Title = title,
        Body = "测试通知",
        FlowId = 100,
        UserId = userId,
        IdempotencyKey = key,
    };
}
