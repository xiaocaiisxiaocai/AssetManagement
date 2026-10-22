using AssetManagement.Domain.Workflow;

namespace AssetManagement.Infrastructure.Workflow;

/// <summary>
/// Resolves a workflow token to a renderable approval node.
///
/// Workflow definitions are editable and older flow instances keep the node
/// ids from the definition that was active when they were created.  When that
/// definition has since been replaced, a historical user-task token no longer
/// exists in the current process.  Keep the persisted token visible instead of
/// silently dropping it from the progress timeline.
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

    public static string CurrentStepState(BpmnNode node) =>
        IsDefinitionMissing(node) ? "unavailable" : "current";

    public static bool HasHistoricalActivity(BpmnToken token)
        => token.Status is BpmnTokenStatus.Completed or BpmnTokenStatus.Skipped &&
           (!string.IsNullOrWhiteSpace(token.Approver)
           || !string.IsNullOrWhiteSpace(token.Opinion)
           || token.History.Count > 0
           || token.SignStates is { Count: > 0 });

    private static bool LooksLikeHistoricalUserTask(BpmnToken token)
        => token.NodeId.StartsWith("Task", StringComparison.OrdinalIgnoreCase)
           || HasHistoricalActivity(token);
}
