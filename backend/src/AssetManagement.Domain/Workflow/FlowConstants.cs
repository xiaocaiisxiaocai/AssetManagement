namespace AssetManagement.Domain.Workflow;

/// <summary>
/// 流程状态常量
/// </summary>
public static class FlowStatus
{
    /// <summary>
    /// 待审批
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    /// 审批已结束或无需审批，等待接收人确认。保管人尚未变更。
    /// </summary>
    public const string Receiving = "receiving";

    /// <summary>
    /// 已通过
    /// </summary>
    public const string Approved = "approved";

    /// <summary>
    /// 已驳回
    /// </summary>
    public const string Rejected = "rejected";

    /// <summary>
    /// 申请人已撤回
    /// </summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>
    /// 料件流转尚未结束：审批中或待接收。资产流程不要使用此判断。
    /// </summary>
    public static bool IsMaterialInProgress(string? status) =>
        string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Receiving, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// BPMN Token 状态常量
/// </summary>
public static class TokenStatus
{
    /// <summary>
    /// 活跃中
    /// </summary>
    public const int Active = 0;

    /// <summary>
    /// 已完成
    /// </summary>
    public const int Completed = 1;

    /// <summary>
    /// 已终止
    /// </summary>
    public const int Terminated = 2;
}

/// <summary>
/// 审批人类型常量
/// </summary>
public static class AssigneeType
{
    /// <summary>
    /// 直属主管
    /// </summary>
    public const string Supervisor = "supervisor";

    /// <summary>
    /// 部门经理
    /// </summary>
    public const string DeptManager = "deptManager";
}
