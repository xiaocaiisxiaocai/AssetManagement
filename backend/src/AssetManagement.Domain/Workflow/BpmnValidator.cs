using System.Xml.Linq;

namespace AssetManagement.Domain.Workflow;

/// <summary>
/// BPMN 流程验证器
/// </summary>
public static class BpmnValidator
{
    /// <summary>
    /// 验证 BPMN XML
    /// </summary>
    public static List<string> Validate(string bpmnXml)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            errors.Add("BPMN XML 不能为空");
            return errors;
        }

        try
        {
            var doc = XDocument.Parse(bpmnXml);
            var ns = doc.Root?.GetDefaultNamespace();
            if (ns == null || ns == XNamespace.None)
            {
                ns = XNamespace.Get("http://www.omg.org/spec/BPMN/20100524/MODEL");
            }
            var camundaNs = XNamespace.Get("http://camunda.org/schema/1.0/bpmn");

            // 检查是否有 process 元素
            var process = doc.Descendants(ns + "process").FirstOrDefault();
            if (process == null)
            {
                errors.Add("BPMN XML 缺少 process 元素");
                return errors;
            }

            // 检查是否有开始事件
            var startEvents = doc.Descendants(ns + "startEvent").ToList();
            if (startEvents.Count == 0)
            {
                errors.Add("流程缺少开始事件（startEvent）");
            }

            // 检查是否有结束事件
            var endEvents = doc.Descendants(ns + "endEvent").ToList();
            if (endEvents.Count == 0)
            {
                errors.Add("流程缺少结束事件（endEvent）");
            }

            // 检查所有 UserTask 是否配置了审批人
            var userTasks = doc.Descendants(ns + "userTask").ToList();
            foreach (var task in userTasks)
            {
                var taskId = task.Attribute("id")?.Value ?? "未知";
                var taskName = task.Attribute("name")?.Value ?? taskId;

                // 检查直接属性
                var assignee = task.Attribute(camundaNs + "assignee")?.Value;
                var candidateUsers = task.Attribute(camundaNs + "candidateUsers")?.Value;
                var candidateGroups = task.Attribute(camundaNs + "candidateGroups")?.Value;

                // 检查 extensionElements 中的 camunda:property
                var extensionElements = task.Element(ns + "extensionElements");
                if (extensionElements != null)
                {
                    var properties = extensionElements.Element(camundaNs + "properties");
                    if (properties != null)
                    {
                        foreach (var prop in properties.Elements(camundaNs + "property"))
                        {
                            var propName = prop.Attribute("name")?.Value;
                            var propValue = prop.Attribute("value")?.Value;

                            if (propName == "assignee" && !string.IsNullOrWhiteSpace(propValue))
                                assignee = propValue;
                            else if (propName == "candidateUsers" && !string.IsNullOrWhiteSpace(propValue))
                                candidateUsers = propValue;
                            else if (propName == "candidateGroups" && !string.IsNullOrWhiteSpace(propValue))
                                candidateGroups = propValue;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(assignee) &&
                    string.IsNullOrWhiteSpace(candidateUsers) &&
                    string.IsNullOrWhiteSpace(candidateGroups))
                {
                    errors.Add($"UserTask '{taskName}' (id: {taskId}) 缺少审批人配置");
                }
            }

            // 检查所有节点是否有连接
            var allNodes = doc.Descendants(ns + "userTask")
                .Concat(doc.Descendants(ns + "serviceTask"))
                .Concat(doc.Descendants(ns + "startEvent"))
                .Concat(doc.Descendants(ns + "endEvent"))
                .Concat(doc.Descendants(ns + "exclusiveGateway"))
                .Concat(doc.Descendants(ns + "parallelGateway"))
                .Concat(doc.Descendants(ns + "inclusiveGateway"))
                .ToList();

            var allFlows = doc.Descendants(ns + "sequenceFlow").ToList();
            var connectedNodes = new HashSet<string>();

            foreach (var flow in allFlows)
            {
                var source = flow.Attribute("sourceRef")?.Value;
                var target = flow.Attribute("targetRef")?.Value;
                if (!string.IsNullOrEmpty(source)) connectedNodes.Add(source);
                if (!string.IsNullOrEmpty(target)) connectedNodes.Add(target);
            }

            foreach (var node in allNodes)
            {
                var nodeId = node.Attribute("id")?.Value;
                var nodeName = node.Attribute("name")?.Value ?? nodeId;
                if (!string.IsNullOrEmpty(nodeId) && !connectedNodes.Contains(nodeId))
                {
                    // 开始事件可以没有入边，结束事件可以没有出边
                    if (node.Name.LocalName != "startEvent" && node.Name.LocalName != "endEvent")
                    {
                        errors.Add($"节点 '{nodeName}' (id: {nodeId}) 没有连接到任何流程分支");
                    }
                }
            }

            // 检查条件表达式的语法（简单检查）
            var conditionFlows = allFlows.Where(f => f.Descendants(ns + "conditionExpression").Any()).ToList();
            foreach (var flow in conditionFlows)
            {
                var flowId = flow.Attribute("id")?.Value ?? "未知";
                var expr = flow.Descendants(ns + "conditionExpression").FirstOrDefault()?.Value?.Trim();

                if (!string.IsNullOrWhiteSpace(expr))
                {
                    // 检查是否是有效的条件表达式格式
                    // 支持: ${applicantDept} == "..." 或 applicantDept == "..."
                    var hasValidSyntax = expr.Contains("applicantDept") && expr.Contains("==");

                    if (!hasValidSyntax)
                    {
                        errors.Add($"SequenceFlow '{flowId}' 的条件表达式语法可能不正确: {expr}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"BPMN XML 解析失败: {ex.Message}");
        }

        return errors;
    }

    /// <summary>
    /// 验证条件表达式（防止注入攻击）
    /// </summary>
    public static bool IsValidConditionExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return true; // 空表达式视为有效（默认分支）
        }

        // 只允许申请部门等值判断，金额条件已下线。
        var pattern = @"^\$\{applicantDept\}\s*==\s*[""'][^""']+[""']$";
        return System.Text.RegularExpressions.Regex.IsMatch(expression.Trim(), pattern);
    }

    /// <summary>
    /// 验证 BPMN XML 安全性（防止 XML 注入）
    /// </summary>
    public static List<string> ValidateSecurity(string bpmnXml)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            return errors;
        }

        // 检查是否包含潜在危险内容
        var dangerousPatterns = new[]
        {
            "<script",
            "javascript:",
            "onclick=",
            "onerror=",
            "onload=",
            "eval(",
            "<!DOCTYPE",
            "<!ENTITY"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (bpmnXml.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"BPMN XML 包含潜在危险内容: {pattern}");
            }
        }

        return errors;
    }
}
