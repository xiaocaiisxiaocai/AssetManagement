using System.Xml.Linq;

namespace AssetManagement.Domain.Workflow;

/// <summary>
/// BPMN 2.0 XML 解析器
/// </summary>
public static class BpmnParser
{
    private static readonly XNamespace BpmnNs = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly XNamespace CamundaNs = "http://camunda.org/schema/1.0/bpmn";

    /// <summary>
    /// 解析 BPMN XML 字符串为流程对象
    /// </summary>
    public static BpmnProcess Parse(string bpmnXml)
    {
        var doc = XDocument.Parse(bpmnXml);
        var processElement = doc.Descendants(BpmnNs + "process").FirstOrDefault()
            ?? throw new ArgumentException("无效的 BPMN XML：缺少 process 元素");

        var process = new BpmnProcess
        {
            Id = processElement.Attribute("id")?.Value ?? "",
            Name = processElement.Attribute("name")?.Value ?? ""
        };

        // 解析所有节点
        ParseStartEvents(processElement, process);
        ParseEndEvents(processElement, process);
        ParseUserTasks(processElement, process);
        ParseServiceTasks(processElement, process);
        ParseExclusiveGateways(processElement, process);
        ParseParallelGateways(processElement, process);
        ParseInclusiveGateways(processElement, process);

        // 解析所有连线
        ParseSequenceFlows(processElement, process);

        return process;
    }

    private static void ParseStartEvents(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "startEvent"))
        {
            process.Nodes.Add(new BpmnNode
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "开始",
                Type = BpmnNodeType.StartEvent,
                Properties = ParseExtensionProperties(element)
            });
        }
    }

    private static void ParseEndEvents(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "endEvent"))
        {
            process.Nodes.Add(new BpmnNode
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "结束",
                Type = BpmnNodeType.EndEvent,
                Properties = ParseExtensionProperties(element)
            });
        }
    }

    private static void ParseUserTasks(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "userTask"))
        {
            var properties = ParseExtensionProperties(element);

            // 解析 Camunda 扩展属性
            var assignee = element.Attribute(CamundaNs + "assignee")?.Value;
            var candidateUsers = element.Attribute(CamundaNs + "candidateUsers")?.Value;
            var candidateGroups = element.Attribute(CamundaNs + "candidateGroups")?.Value;
            var approvalMode = element.Attribute(CamundaNs + "approvalMode")?.Value;

            if (!string.IsNullOrEmpty(assignee))
                properties["assignee"] = assignee;
            if (!string.IsNullOrEmpty(candidateUsers))
                properties["candidateUsers"] = candidateUsers;
            if (!string.IsNullOrEmpty(candidateGroups))
                properties["candidateGroups"] = candidateGroups;
            if (!string.IsNullOrEmpty(approvalMode))
                properties["approvalMode"] = approvalMode;

            process.Nodes.Add(new BpmnNode
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "用户任务",
                Type = BpmnNodeType.UserTask,
                Properties = properties
            });
        }
    }

    private static void ParseServiceTasks(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "serviceTask"))
        {
            process.Nodes.Add(new BpmnNode
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "服务任务",
                Type = BpmnNodeType.ServiceTask,
                Properties = ParseExtensionProperties(element)
            });
        }
    }

    private static void ParseExclusiveGateways(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "exclusiveGateway"))
        {
            process.Nodes.Add(new BpmnNode
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "排他网关",
                Type = BpmnNodeType.ExclusiveGateway,
                Properties = ParseExtensionProperties(element)
            });
        }
    }

    private static void ParseParallelGateways(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "parallelGateway"))
        {
            process.Nodes.Add(new BpmnNode
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "并行网关",
                Type = BpmnNodeType.ParallelGateway,
                Properties = ParseExtensionProperties(element)
            });
        }
    }

    private static void ParseInclusiveGateways(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "inclusiveGateway"))
        {
            process.Nodes.Add(new BpmnNode
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "包容网关",
                Type = BpmnNodeType.InclusiveGateway,
                Properties = ParseExtensionProperties(element)
            });
        }
    }

    private static void ParseSequenceFlows(XElement processElement, BpmnProcess process)
    {
        foreach (var element in processElement.Descendants(BpmnNs + "sequenceFlow"))
        {
            var conditionElement = element.Element(BpmnNs + "conditionExpression");
            var conditionExpression = conditionElement?.Value?.Trim();

            process.Flows.Add(new BpmnFlow
            {
                Id = element.Attribute("id")?.Value ?? "",
                Name = element.Attribute("name")?.Value ?? "",
                SourceRef = element.Attribute("sourceRef")?.Value ?? "",
                TargetRef = element.Attribute("targetRef")?.Value ?? "",
                ConditionExpression = conditionExpression
            });
        }
    }

    /// <summary>
    /// 解析扩展属性（extensionElements）
    /// </summary>
    private static Dictionary<string, string> ParseExtensionProperties(XElement element)
    {
        var properties = new Dictionary<string, string>();

        var extensionElements = element.Element(BpmnNs + "extensionElements");
        if (extensionElements == null)
            return properties;

        // 解析 Camunda 属性
        var camundaProperties = extensionElements.Element(CamundaNs + "properties");
        if (camundaProperties != null)
        {
            foreach (var prop in camundaProperties.Elements(CamundaNs + "property"))
            {
                var name = prop.Attribute("name")?.Value;
                var value = prop.Attribute("value")?.Value;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                {
                    properties[name] = value;
                }
            }
        }

        return properties;
    }

    /// <summary>
    /// 验证 BPMN 流程定义的完整性
    /// </summary>
    public static List<string> Validate(BpmnProcess process)
    {
        var errors = new List<string>();

        // 检查必须有开始事件
        if (!process.Nodes.Any(n => n.Type == BpmnNodeType.StartEvent))
        {
            errors.Add("流程必须包含开始事件（StartEvent）");
        }

        // 检查必须有结束事件
        if (!process.Nodes.Any(n => n.Type == BpmnNodeType.EndEvent))
        {
            errors.Add("流程必须包含结束事件（EndEvent）");
        }

        // 检查连线引用的节点是否存在
        var nodeIds = process.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var flow in process.Flows)
        {
            if (!nodeIds.Contains(flow.SourceRef))
            {
                errors.Add($"连线 {flow.Id} 的源节点 {flow.SourceRef} 不存在");
            }
            if (!nodeIds.Contains(flow.TargetRef))
            {
                errors.Add($"连线 {flow.Id} 的目标节点 {flow.TargetRef} 不存在");
            }
        }

        // 检查开始事件不能有入边
        foreach (var startNode in process.Nodes.Where(n => n.Type == BpmnNodeType.StartEvent))
        {
            if (process.GetIncomingFlows(startNode.Id).Any())
            {
                errors.Add($"开始事件 {startNode.Id} 不能有入边");
            }
        }

        // 检查结束事件不能有出边
        foreach (var endNode in process.Nodes.Where(n => n.Type == BpmnNodeType.EndEvent))
        {
            if (process.GetOutgoingFlows(endNode.Id).Any())
            {
                errors.Add($"结束事件 {endNode.Id} 不能有出边");
            }
        }

        foreach (var node in process.Nodes.Where(n =>
                     n.Type is BpmnNodeType.StartEvent or BpmnNodeType.UserTask or BpmnNodeType.ServiceTask))
        {
            var outgoingCount = process.GetOutgoingFlows(node.Id).Count;
            if (outgoingCount != 1)
            {
                errors.Add($"节点 {node.Name}（{node.Id}）必须且只能有一个出边");
            }
        }

        foreach (var gateway in process.Nodes.Where(n => n.Type is BpmnNodeType.ExclusiveGateway or BpmnNodeType.InclusiveGateway))
        {
            var outgoingFlows = process.GetOutgoingFlows(gateway.Id);
            if (outgoingFlows.Count < 2)
            {
                errors.Add($"网关 {gateway.Name}（{gateway.Id}）至少需要两个出边");
            }

            if (gateway.Type == BpmnNodeType.ExclusiveGateway &&
                outgoingFlows.All(flow => !string.IsNullOrWhiteSpace(flow.ConditionExpression)))
            {
                errors.Add($"排他网关 {gateway.Name}（{gateway.Id}）需要保留一个无条件默认分支");
            }
        }

        // 检查用户任务必须有审批人配置
        foreach (var userTask in process.Nodes.Where(n => n.Type == BpmnNodeType.UserTask))
        {
            if (!userTask.Properties.ContainsKey("assignee") &&
                !userTask.Properties.ContainsKey("candidateUsers") &&
                !userTask.Properties.ContainsKey("candidateGroups"))
            {
                errors.Add($"用户任务 {userTask.Name}（{userTask.Id}）必须配置审批人");
            }
        }

        return errors;
    }
}
