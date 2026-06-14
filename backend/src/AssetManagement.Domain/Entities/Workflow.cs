using AssetManagement.Domain.Workflow;

namespace AssetManagement.Domain.Entities;

public class Workflow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string BizType { get; set; } = "";
    public List<WorkflowNode> Nodes { get; set; } = new();
}
