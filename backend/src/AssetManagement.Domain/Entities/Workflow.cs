namespace AssetManagement.Domain.Entities;

public class Workflow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string BizType { get; set; } = "";
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// BPMN 2.0 XML 定义（新架构使用此字段）
    /// </summary>
    public string? BpmnXml { get; set; }
}
