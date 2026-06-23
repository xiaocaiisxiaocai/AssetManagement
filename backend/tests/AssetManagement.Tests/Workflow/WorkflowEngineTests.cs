using AssetManagement.Domain.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.Workflow;

/// <summary>
/// BPMN 引擎单元测试 - 测试核心流程执行逻辑
/// 注意: 这些是纯函数单元测试,不依赖数据库或 Web 框架
/// 更完整的集成测试在 BpmnEngineRegressionTests 中
/// </summary>
public class BpmnEngineTests
{
    [Fact]
    public void Start_initializes_flow_and_advances_to_first_user_task()
    {
        var bpmn = SimpleLinearBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);

        flow.Status.Should().Be("pending");
        flow.CurrentNodeIds.Should().ContainSingle()
            .Which.Should().Be("Task_Review", "流程应推进到第一个 UserTask");
        flow.BpmnTokens.Should().ContainKey("Task_Review");
        flow.BpmnTokens["Task_Review"].Status.Should().Be(BpmnTokenStatus.Active);
    }

    [Fact]
    public void Approve_advances_to_next_task()
    {
        var bpmn = TwoTaskBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);
        BpmnEngine.Approve(flow, process, "Task_First", "张三", "同意");

        flow.BpmnTokens["Task_First"].Status.Should().Be(BpmnTokenStatus.Completed);
        flow.BpmnTokens["Task_First"].Approver.Should().Be("张三");
        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_Second");
    }

    [Fact]
    public void Approve_on_last_task_completes_flow()
    {
        var bpmn = SimpleLinearBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);
        BpmnEngine.Approve(flow, process, "Task_Review", "李四", "通过");

        flow.Status.Should().Be("approved");
        flow.CurrentNodeIds.Should().BeEmpty("流程已完成");
    }

    [Fact]
    public void Reject_terminates_flow()
    {
        var bpmn = SimpleLinearBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);
        BpmnEngine.Reject(flow, "Task_Review", "王五", "不符合要求");

        flow.Status.Should().Be("rejected");
        flow.CurrentNodeIds.Should().BeEmpty();
        flow.BpmnTokens["Task_Review"].Status.Should().Be(BpmnTokenStatus.Completed);
        flow.BpmnTokens["Task_Review"].Opinion.Should().Contain("驳回");
    }

    [Fact]
    public void Parallel_gateway_creates_multiple_tokens()
    {
        var bpmn = ParallelGatewayBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);

        // 并行网关应该创建两个并行的 Token
        flow.CurrentNodeIds.Should().HaveCount(2);
        flow.CurrentNodeIds.Should().Contain("Task_A");
        flow.CurrentNodeIds.Should().Contain("Task_B");
        flow.BpmnTokens["Task_A"].Status.Should().Be(BpmnTokenStatus.Active);
        flow.BpmnTokens["Task_B"].Status.Should().Be(BpmnTokenStatus.Active);
    }

    [Fact]
    public void Parallel_gateway_waits_for_all_branches()
    {
        var bpmn = ParallelGatewayBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);
        BpmnEngine.Approve(flow, process, "Task_A", "审批人A", "同意");

        // 只完成一个分支,流程应继续等待另一个分支
        flow.Status.Should().Be("pending");
        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_B");

        BpmnEngine.Approve(flow, process, "Task_B", "审批人B", "同意");

        // 两个分支都完成后,流程才完成
        flow.Status.Should().Be("approved");
        flow.CurrentNodeIds.Should().BeEmpty();
    }

    [Fact]
    public void Exclusive_gateway_selects_one_branch_based_on_condition()
    {
        var bpmn = ExclusiveGatewayBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow { ApplicantDept = "技术部" };

        BpmnEngine.Start(flow, process);

        // 排他网关应选择技术部分支
        flow.CurrentNodeIds.Should().ContainSingle()
            .Which.Should().Be("Task_DeptA", "applicantDept == '技术部' 应走部门A分支");
    }

    [Fact]
    public void Countersign_requires_all_signers()
    {
        var bpmn = CountersignBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);

        // 会签节点初始化后,SignStates 应包含所有会签人
        var token = flow.BpmnTokens["Task_Countersign"];
        token.SignStates.Should().ContainKeys("张三", "李四", "王五");
        token.SignStates.Values.Should().OnlyContain(signed => signed == false);

        // 第一个人审批
        BpmnEngine.Approve(flow, process, "Task_Countersign", "张三", "同意");
        flow.Status.Should().Be("pending", "还有人未签署");
        token.SignStates["张三"].Should().BeTrue();

        // 第二个人审批
        BpmnEngine.Approve(flow, process, "Task_Countersign", "李四", "同意");
        flow.Status.Should().Be("pending", "还有人未签署");

        // 最后一个人审批,流程完成
        BpmnEngine.Approve(flow, process, "Task_Countersign", "王五", "同意");
        flow.Status.Should().Be("approved", "所有人都签署后流程应完成");
    }

    [Fact]
    public void Approve_on_non_existent_node_throws()
    {
        var bpmn = SimpleLinearBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);

        var act = () => BpmnEngine.Approve(flow, process, "NonExistentNode", "张三", "同意");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*不存在活跃的 Token*");
    }

    #region BPMN 测试数据辅助方法

    private static string SimpleLinearBpmn() => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""simple"" isExecutable=""true"">
    <bpmn:startEvent id=""Start"" />
    <bpmn:userTask id=""Task_Review"" name=""审核"" />
    <bpmn:endEvent id=""End"" />
    <bpmn:sequenceFlow id=""F1"" sourceRef=""Start"" targetRef=""Task_Review"" />
    <bpmn:sequenceFlow id=""F2"" sourceRef=""Task_Review"" targetRef=""End"" />
  </bpmn:process>
</bpmn:definitions>";

    private static string TwoTaskBpmn() => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""twoTask"" isExecutable=""true"">
    <bpmn:startEvent id=""Start"" />
    <bpmn:userTask id=""Task_First"" name=""第一步"" />
    <bpmn:userTask id=""Task_Second"" name=""第二步"" />
    <bpmn:endEvent id=""End"" />
    <bpmn:sequenceFlow id=""F1"" sourceRef=""Start"" targetRef=""Task_First"" />
    <bpmn:sequenceFlow id=""F2"" sourceRef=""Task_First"" targetRef=""Task_Second"" />
    <bpmn:sequenceFlow id=""F3"" sourceRef=""Task_Second"" targetRef=""End"" />
  </bpmn:process>
</bpmn:definitions>";

    private static string ParallelGatewayBpmn() => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""parallel"" isExecutable=""true"">
    <bpmn:startEvent id=""Start"" />
    <bpmn:parallelGateway id=""Fork"" />
    <bpmn:userTask id=""Task_A"" name=""分支A"" />
    <bpmn:userTask id=""Task_B"" name=""分支B"" />
    <bpmn:parallelGateway id=""Join"" />
    <bpmn:endEvent id=""End"" />
    <bpmn:sequenceFlow id=""F1"" sourceRef=""Start"" targetRef=""Fork"" />
    <bpmn:sequenceFlow id=""F2"" sourceRef=""Fork"" targetRef=""Task_A"" />
    <bpmn:sequenceFlow id=""F3"" sourceRef=""Fork"" targetRef=""Task_B"" />
    <bpmn:sequenceFlow id=""F4"" sourceRef=""Task_A"" targetRef=""Join"" />
    <bpmn:sequenceFlow id=""F5"" sourceRef=""Task_B"" targetRef=""Join"" />
    <bpmn:sequenceFlow id=""F6"" sourceRef=""Join"" targetRef=""End"" />
  </bpmn:process>
</bpmn:definitions>";

    private static string ExclusiveGatewayBpmn() => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <bpmn:process id=""exclusive"" isExecutable=""true"">
    <bpmn:startEvent id=""Start"" />
    <bpmn:exclusiveGateway id=""Gateway"" />
    <bpmn:userTask id=""Task_DeptA"" name=""部门A审批"" />
    <bpmn:userTask id=""Task_DeptB"" name=""部门B审批"" />
    <bpmn:endEvent id=""End1"" />
    <bpmn:endEvent id=""End2"" />
    <bpmn:sequenceFlow id=""F1"" sourceRef=""Start"" targetRef=""Gateway"" />
    <bpmn:sequenceFlow id=""F2"" sourceRef=""Gateway"" targetRef=""Task_DeptA"">
      <bpmn:conditionExpression>${applicantDept} == &quot;技术部&quot;</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""F3"" sourceRef=""Gateway"" targetRef=""Task_DeptB"">
      <bpmn:conditionExpression>${applicantDept} == &quot;行政部&quot;</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""F4"" sourceRef=""Task_DeptA"" targetRef=""End1"" />
    <bpmn:sequenceFlow id=""F5"" sourceRef=""Task_DeptB"" targetRef=""End2"" />
  </bpmn:process>
</bpmn:definitions>";

    private static string CountersignBpmn() => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn"">
  <bpmn:process id=""countersign"" isExecutable=""true"">
    <bpmn:startEvent id=""Start"" />
    <bpmn:userTask id=""Task_Countersign"" name=""会签审批"">
      <bpmn:extensionElements>
        <camunda:properties>
          <camunda:property name=""approvalMode"" value=""all"" />
          <camunda:property name=""assignee"" value=""张三,李四,王五"" />
        </camunda:properties>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id=""End"" />
    <bpmn:sequenceFlow id=""F1"" sourceRef=""Start"" targetRef=""Task_Countersign"" />
    <bpmn:sequenceFlow id=""F2"" sourceRef=""Task_Countersign"" targetRef=""End"" />
  </bpmn:process>
</bpmn:definitions>";

    #endregion

    /// <summary>
    /// 测试用的流程实例 - 实现 IBpmnFlowInstance 接口
    /// </summary>
    private class TestFlow : IBpmnFlowInstance
    {
        public Dictionary<string, BpmnToken> BpmnTokens { get; set; } = new();
        public List<string> CurrentNodeIds { get; set; } = new();
        public string Status { get; set; } = "pending";
        public string? ApplicantDept { get; set; }
    }
}
