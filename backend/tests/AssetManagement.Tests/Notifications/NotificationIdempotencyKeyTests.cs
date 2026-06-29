using System.Reflection;
using FluentAssertions;

namespace AssetManagement.Tests.Notifications;

public class NotificationIdempotencyKeyTests
{
    [Fact]
    public void Pending_approval_key_stays_within_database_limit_for_long_bpmn_node_id()
    {
        var key = BuildPendingApprovalKey(
            "material_approval_pending",
            flowId: 123456789,
            nodeId: new string('x', 500),
            userId: 987654321,
            nodeVersion: new DateTime(2026, 6, 29, 12, 30, 45, DateTimeKind.Utc));

        key.Length.Should().BeLessThanOrEqualTo(100, "notifications.IdempotencyKey 当前数据库字段长度为 100");
        key.Should().StartWith("material_approval_pending_123456789_");
        key.Should().EndWith("_987654321_20260629123045");
    }

    [Fact]
    public void Pending_approval_key_keeps_node_identity_in_hash()
    {
        var first = BuildPendingApprovalKey("approval_pending", 1, "Task_first", 2, DateTime.UnixEpoch);
        var second = BuildPendingApprovalKey("approval_pending", 1, "Task_second", 2, DateTime.UnixEpoch);

        first.Should().NotBe(second, "不同 BPMN 节点不能共用同一个通知幂等键");
    }

    private static string BuildPendingApprovalKey(string prefix, int flowId, string nodeId, int userId, DateTime nodeVersion)
    {
        var type = Type.GetType("AssetManagement.Infrastructure.Notifications.NotificationIdempotencyKeys, AssetManagement.Infrastructure");
        type.Should().NotBeNull("通知幂等 key 需要集中生成，避免各业务服务重复拼接超长 key");

        var method = type!.GetMethod("PendingApproval", BindingFlags.Public | BindingFlags.Static);
        method.Should().NotBeNull();

        return method!.Invoke(null, new object[] { prefix, flowId, nodeId, userId, nodeVersion })
            .Should().BeOfType<string>().Subject;
    }
}
