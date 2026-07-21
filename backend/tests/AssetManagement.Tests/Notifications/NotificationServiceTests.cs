using AssetManagement.Application.Notifications;
using AssetManagement.Domain.Entities;
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
        var userIds = await AddUsersAsync(2);

        await service.CreateBatchAsync(new[]
        {
            NewRequest("dup-key", userIds[0], title: "第一次"),
            NewRequest("dup-key", userIds[0], title: "重复项"),
            NewRequest("unique-key", userIds[1], title: "独立通知"),
        });

        var notifications = await _db.Notifications
            .OrderBy(x => x.Id)
            .Select(x => new { x.IdempotencyKey, x.Title, x.UserId })
            .ToListAsync();

        notifications.Should().HaveCount(2);
        notifications.Select(x => x.IdempotencyKey)
            .Should().BeEquivalentTo("dup-key", "unique-key");
        notifications.Should().Contain(x => x.Title == "独立通知" && x.UserId == userIds[1]);
    }

    [Fact]
    public async Task Create_keeps_context_usable_after_duplicate_idempotency_key()
    {
        var service = new NotificationService(_db);
        var userIds = await AddUsersAsync(3);

        await service.CreateAsync(NewRequest("dup-key", userIds[0], title: "第一次"));

        // 模拟并发重复键导致 SaveChanges 失败时，ChangeTracker 中已有失败的 Added 实体。
        _db.Notifications.Add(new Domain.Entities.Notification
        {
            Type = "approval_pending",
            Title = "重复插入",
            Body = "模拟并发重复键",
            FlowId = 101,
            UserId = userIds[1],
            IdempotencyKey = "dup-key",
            CreatedAt = DateTime.UtcNow,
        });

        await service.CreateAsync(NewRequest("unique-key", userIds[1], title: "触发冲突的同批通知"));
        await service.CreateAsync(NewRequest("next-key", userIds[2], title: "后续通知"));

        var notifications = await _db.Notifications
            .OrderBy(x => x.Id)
            .Select(x => new { x.IdempotencyKey, x.Title, x.UserId })
            .ToListAsync();

        notifications.Should().HaveCount(2);
        notifications.Should().Contain(x => x.IdempotencyKey == "dup-key" && x.UserId == userIds[0]);
        notifications.Should().Contain(x => x.IdempotencyKey == "next-key" && x.UserId == userIds[2]);
    }

    [Fact]
    public async Task Notification_list_uses_id_as_stable_tie_breaker_for_same_created_time()
    {
        var service = new NotificationService(_db);
        var userId = (await AddUsersAsync(1))[0];
        var createdAt = DateTime.UtcNow;
        var first = new Notification
        {
            Type = "approval_pending",
            Title = "同时间第一条",
            Body = "测试稳定排序",
            FlowId = 201,
            UserId = userId,
            CreatedAt = createdAt
        };
        var second = new Notification
        {
            Type = "approval_pending",
            Title = "同时间第二条",
            Body = "测试稳定排序",
            FlowId = 202,
            UserId = userId,
            CreatedAt = createdAt
        };
        _db.Notifications.AddRange(first, second);
        await _db.SaveChangesAsync();

        var notifications = await service.GetMyNotificationsAsync(userId);

        notifications.Select(notification => notification.Id)
            .Should().ContainInOrder(second.Id, first.Id);
    }

    [Fact]
    public async Task Clear_removes_only_current_users_notifications()
    {
        var service = new NotificationService(_db);
        var userIds = await AddUsersAsync(2);
        await service.CreateBatchAsync(new[]
        {
            NewRequest("user-1-a", userIds[0], title: "用户一通知A"),
            NewRequest("user-1-b", userIds[0], title: "用户一通知B"),
            NewRequest("user-2-a", userIds[1], title: "用户二通知"),
        });

        await service.ClearAsync(userIds[0]);

        var remaining = await _db.Notifications
            .Select(x => new { x.UserId, x.Title })
            .ToListAsync();
        remaining.Should().ContainSingle();
        remaining[0].UserId.Should().Be(userIds[1]);
        remaining[0].Title.Should().Be("用户二通知");
    }

    [Fact]
    public async Task Create_truncates_user_visible_derived_text_to_database_limits()
    {
        var service = new NotificationService(_db);
        var userId = (await AddUsersAsync(1))[0];

        await service.CreateAsync(new CreateNotificationRequest
        {
            Type = "overdue",
            Title = new string('标', 250),
            Body = new string('内', 700),
            FlowId = 1,
            UserId = userId,
            IdempotencyKey = "bounded-derived-content",
        });

        var saved = await _db.Notifications.SingleAsync();
        saved.Title.Should().HaveLength(200);
        saved.Body.Should().HaveLength(500);
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

    private async Task<int[]> AddUsersAsync(int count)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var users = Enumerable.Range(1, count)
            .Select(index => new User
            {
                EmployeeNo = $"NT{suffix}{index}",
                Name = $"通知测试用户{index}",
                PasswordHash = "test",
                IsActive = true,
            })
            .ToArray();
        _db.Users.AddRange(users);
        await _db.SaveChangesAsync();
        return users.Select(x => x.Id).ToArray();
    }
}
