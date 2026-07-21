using AssetManagement.Application.Notifications;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetManagement.Tests.Notifications;

public class OverdueNotificationWorkerTests : MySqlFixtureBase
{
    [Fact]
    public async Task Scan_delegates_the_whole_batch_to_idempotent_notification_service()
    {
        var user = new User { EmployeeNo = "OVERDUE-USER", Name = "原借用人", PasswordHash = "x" };
        var currentCustodian = new User
        {
            EmployeeNo = "OVERDUE-CURRENT",
            Name = "转让接收人",
            PasswordHash = "x"
        };
        var category = new AssetCategory { Code = "OD", CodeSeg = "OD" };
        var workflow = new Domain.Entities.Workflow { Name = "借用审批", BizType = "borrow", IsActive = true };
        _db.AddRange(user, currentCustodian, category, workflow);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            AssetNo = "OD-001",
            Name = "逾期资产",
            CategoryId = category.Id,
            Status = AssetStatus.Borrowed,
            CustodianId = currentCustodian.Id,
            Quantity = 1,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        _db.ApprovalFlows.Add(new ApprovalFlow
        {
            FlowNo = "OD-FLOW-001",
            BizType = "borrow",
            WorkflowId = workflow.Id,
            AssetId = asset.Id,
            AssetNo = asset.AssetNo,
            AssetName = asset.Name,
            ApplicantId = user.Id,
            Applicant = user.Name,
            Status = "approved",
            ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            ApplyTime = DateTime.UtcNow.AddDays(-2),
            Deadline = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var capture = new CapturingNotificationService();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString)));
        services.AddScoped<INotificationService>(_ => capture);
        await using var provider = services.BuildServiceProvider();
        var worker = new OverdueNotificationWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OverdueNotificationWorker>.Instance);

        await worker.ScanAndNotifyAsync();

        capture.BatchCalls.Should().Be(1);
        capture.Requests.Should().ContainSingle();
        capture.Requests[0].Type.Should().Be("overdue");
        capture.Requests[0].UserId.Should().Be(currentCustodian.Id,
            "资产转让后的到期和逾期提醒必须发送给当前保管人");
    }

    private sealed class CapturingNotificationService : INotificationService
    {
        public int BatchCalls { get; private set; }
        public List<CreateNotificationRequest> Requests { get; } = new();

        public Task<int> CreateBatchAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            Requests.AddRange(requests);
            return Task.FromResult(Requests.Count);
        }

        public Task<bool> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<List<NotificationDto>> GetMyNotificationsAsync(int userId, bool unreadOnly = false)
            => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(int userId) => throw new NotSupportedException();
        public Task MarkReadAsync(int id, int userId) => throw new NotSupportedException();
        public Task MarkAllReadAsync(int userId) => throw new NotSupportedException();
        public Task ClearAsync(int userId) => throw new NotSupportedException();
    }
}
