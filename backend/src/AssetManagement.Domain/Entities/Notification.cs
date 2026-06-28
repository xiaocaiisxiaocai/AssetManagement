namespace AssetManagement.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    /// <summary>通知类型：due_soon_1d / due_soon_3d / overdue</summary>
    public string Type { get; set; } = "";

    public string Title { get; set; } = "";
    public string Body { get; set; } = "";

    /// <summary>关联借用流程 ID</summary>
    public int FlowId { get; set; }

    /// <summary>通知目标用户 ID（借用人）</summary>
    public int UserId { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>幂等键：Type + FlowId + 日期，防止每天重复生成；无幂等需求时为 null</summary>
    public string? IdempotencyKey { get; set; }
}
