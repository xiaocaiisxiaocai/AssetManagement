using System.Security.Cryptography;
using System.Text;

namespace AssetManagement.Infrastructure.Notifications;

public static class NotificationIdempotencyKeys
{
    public static string PendingApproval(string prefix, int flowId, string nodeId, int userId, DateTime nodeVersion)
    {
        var nodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nodeId)))[..16].ToLowerInvariant();
        return $"{prefix}_{flowId}_{nodeHash}_{userId}_{nodeVersion:yyyyMMddHHmmss}";
    }
}
