using AssetManagement.Domain.Workflow;

namespace AssetManagement.Infrastructure.Workflow;

/// <summary>
/// Resolves a workflow token to a renderable approval node.
///
/// Workflow definitions are editable and older flow instances keep the node
/// ids from the definition that was active when they were created.  When that
/// definition has since been replaced, a historical user-task token no longer
/// exists in the current process.  Keep the persisted token visible instead of
/// silently dropping it.  Seeds use Task_ ids; bpmn-js uses Activity_.  An
/// active token is still a current approval step even when its id matches
/// neither prefix and it has no approval history yet.
/// </summary>
internal static class BpmnProgressNodeResolver
{
    public const string DefinitionMissingKey = "definitionMissing";

    public static BpmnNode? Resolve(BpmnProcess process, BpmnToken token)
    {
        var currentNode = process.FindNode(token.NodeId);
        if (currentNode is not null) return currentNode;
        if (!LooksLikeHistoricalUserTask(token)) return null;

        var properties = new Dictionary<string, string>();
        if (token.Status == BpmnTokenStatus.Active)
            properties[DefinitionMissingKey] = "true";

        return new BpmnNode
        {
            Id = token.NodeId,
            Name = string.IsNullOrWhiteSpace(token.NodeName) ? token.NodeId : token.NodeName,
            Type = BpmnNodeType.UserTask,
            Properties = properties
        };
    }

    public static bool IsDefinitionMissing(BpmnNode? node) =>
        node?.Properties.GetValueOrDefault(DefinitionMissingKey) == "true";

    /// <summary>
    /// 进行中的当前节点在所绑定定义里无法继续审批。没有节点时不是这种残留。
    /// </summary>
    public static bool HasUnavailableCurrentNode(
        BpmnProcess? process,
        IEnumerable<string> currentNodeIds,
        IReadOnlyDictionary<string, BpmnToken>? tokens)
    {
        foreach (var nodeId in currentNodeIds)
        {
            BpmnToken? token = null;
            tokens?.TryGetValue(nodeId, out token);
            if (process is null) return true;
            var node = token is null ? process.FindNode(nodeId) : Resolve(process, token);
            if (node is null || node.Type != BpmnNodeType.UserTask || IsDefinitionMissing(node))
                return true;
        }
        return false;
    }

    public static string CurrentStepState(BpmnNode node) =>
        IsDefinitionMissing(node) ? "unavailable" : "current";

    public static bool HasHistoricalActivity(BpmnToken token)
        => token.Status is BpmnTokenStatus.Completed or BpmnTokenStatus.Skipped &&
           (!string.IsNullOrWhiteSpace(token.Approver)
           || !string.IsNullOrWhiteSpace(token.Opinion)
           || token.History.Count > 0
           || token.SignStates is { Count: > 0 });

    private static bool LooksLikeHistoricalUserTask(BpmnToken token)
        => token.Status == BpmnTokenStatus.Active
           || token.NodeId.StartsWith("Task", StringComparison.OrdinalIgnoreCase)
           || token.NodeId.StartsWith("Activity", StringComparison.OrdinalIgnoreCase)
           || HasHistoricalActivity(token);
}
