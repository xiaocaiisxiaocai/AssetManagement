using AssetManagement.Domain.Workflow;
using AssetManagement.Infrastructure.Workflow;
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
    public void Flow_record_comment_truncation_respects_database_boundary()
    {
        WorkflowService.Truncate(new string('意', 501), 500)
            .Should().HaveLength(500);
    }

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
    public void Start_records_active_user_task_started_time()
    {
        var bpmn = SimpleLinearBpmn();
        var process = BpmnParser.Parse(bpmn);
        var flow = new TestFlow();

        BpmnEngine.Start(flow, process);

        flow.BpmnTokens["Task_Review"].StartedAt.Should().NotBeNull();
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
    public void Exclusive_gateway_merge_with_single_outgoing_is_valid_and_completes()
    {
        var process = BpmnParser.Parse(ExclusiveMergeBpmn());
        BpmnParser.Validate(process).Should().BeEmpty();
        var flow = new TestFlow { Context = new() { ["approved"] = "true" } };

        BpmnEngine.Start(flow, process);
        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_A");
        BpmnEngine.Approve(flow, process, "Task_A", "1");

        flow.Status.Should().Be("approved");
    }

    [Fact]
    public void Inclusive_gateway_merge_waits_only_for_activated_branches()
    {
        var process = BpmnParser.Parse(InclusiveMergeBpmn());
        BpmnParser.Validate(process).Should().BeEmpty();
        var flow = new TestFlow
        {
            Context = new() { ["needA"] = "true", ["needB"] = "true" }
        };

        BpmnEngine.Start(flow, process);
        flow.CurrentNodeIds.Should().BeEquivalentTo("Task_A", "Task_B");
        BpmnEngine.Approve(flow, process, "Task_A", "1");
        flow.Status.Should().Be("pending");
        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_B");
        BpmnEngine.Approve(flow, process, "Task_B", "2");
        flow.Status.Should().Be("approved");

        var oneBranch = new TestFlow
        {
            Context = new() { ["needA"] = "true", ["needB"] = "false" }
        };
        BpmnEngine.Start(oneBranch, process);
        oneBranch.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_A");
        BpmnEngine.Approve(oneBranch, process, "Task_A", "1");
        oneBranch.Status.Should().Be("approved");
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
    public void Applicant_department_not_equals_condition_matches_runtime_validator_contract()
    {
        var process = BpmnParser.Parse(DepartmentNotEqualsBpmn());
        BpmnValidator.Validate(DepartmentNotEqualsBpmn()).Should().BeEmpty();
        var flow = new TestFlow { ApplicantDept = "行政部" };

        BpmnEngine.Start(flow, process);

        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_Other");
    }

    [Fact]
    public void Inclusive_join_does_not_wait_for_branch_whose_actual_route_bypasses_join()
    {
        var process = BpmnParser.Parse(InclusiveConditionalBypassBpmn());
        var flow = new TestFlow
        {
            Context = new() { ["joinB"] = "false" }
        };

        BpmnEngine.Start(flow, process);
        BpmnEngine.Approve(flow, process, "Task_A", "1");
        BpmnEngine.Approve(flow, process, "Task_B", "2");

        flow.Status.Should().Be("approved");
        flow.BpmnTokens.Values.Should().NotContain(x => x.Status == BpmnTokenStatus.Waiting);
    }

    [Fact]
    public void Parser_rejects_reachable_parallel_gateway_without_outgoing_path()
    {
        var process = BpmnParser.Parse(DeadEndParallelGatewayBpmn());

        BpmnParser.Validate(process).Should().Contain(x => x.Contains("并行网关"));
    }

    [Fact]
    public void Parser_rejects_parallel_branches_that_collapse_into_the_same_user_task()
    {
        const string bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <bpmn:process id="parallel-collapse">
                <bpmn:startEvent id="Start"/><bpmn:parallelGateway id="Fork"/>
                <bpmn:userTask id="Task" camunda:assignee="user:1"/><bpmn:endEvent id="End"/>
                <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Fork"/>
                <bpmn:sequenceFlow id="f2" sourceRef="Fork" targetRef="Task"/>
                <bpmn:sequenceFlow id="f3" sourceRef="Fork" targetRef="Task"/>
                <bpmn:sequenceFlow id="f4" sourceRef="Task" targetRef="End"/>
              </bpmn:process>
            </bpmn:definitions>
            """;

        var errors = BpmnParser.Validate(BpmnParser.Parse(bpmn));

        errors.Should().Contain(error => error.Contains("并行分支不能同时进入"));
    }

    [Fact]
    public void Parser_allows_exclusive_branches_to_merge_into_the_same_user_task()
    {
        const string bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <bpmn:process id="exclusive-merge">
                <bpmn:startEvent id="Start"/><bpmn:exclusiveGateway id="Route"/>
                <bpmn:userTask id="A" camunda:assignee="user:1"/><bpmn:userTask id="B" camunda:assignee="user:1"/>
                <bpmn:userTask id="Review" camunda:assignee="user:1"/><bpmn:endEvent id="End"/>
                <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Route"/>
                <bpmn:sequenceFlow id="f2" sourceRef="Route" targetRef="A"><bpmn:conditionExpression>${route} == 'a'</bpmn:conditionExpression></bpmn:sequenceFlow>
                <bpmn:sequenceFlow id="f3" sourceRef="Route" targetRef="B"/><bpmn:sequenceFlow id="f4" sourceRef="A" targetRef="Review"/>
                <bpmn:sequenceFlow id="f5" sourceRef="B" targetRef="Review"/><bpmn:sequenceFlow id="f6" sourceRef="Review" targetRef="End"/>
              </bpmn:process>
            </bpmn:definitions>
            """;

        BpmnParser.Validate(BpmnParser.Parse(bpmn)).Should().BeEmpty();
    }

    [Fact]
    public void Parser_rejects_parallel_branches_joined_only_by_exclusive_gateway()
    {
        const string bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <bpmn:process id="parallel-exclusive-merge">
                <bpmn:startEvent id="Start"/><bpmn:parallelGateway id="Fork"/><bpmn:exclusiveGateway id="Merge"/>
                <bpmn:userTask id="Review" camunda:assignee="user:1"/><bpmn:endEvent id="End"/>
                <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Fork"/>
                <bpmn:sequenceFlow id="f2" sourceRef="Fork" targetRef="Merge"/>
                <bpmn:sequenceFlow id="f3" sourceRef="Fork" targetRef="Merge"/>
                <bpmn:sequenceFlow id="f4" sourceRef="Merge" targetRef="Review"/>
                <bpmn:sequenceFlow id="f5" sourceRef="Review" targetRef="End"/>
              </bpmn:process>
            </bpmn:definitions>
            """;

        BpmnParser.Validate(BpmnParser.Parse(bpmn))
            .Should().Contain(error => error.Contains("并行分支不能同时进入"));
    }

    [Fact]
    public void Parser_rejects_inclusive_branches_that_collapse_into_the_same_user_task()
    {
        const string bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <bpmn:process id="inclusive-collapse">
                <bpmn:startEvent id="Start"/><bpmn:inclusiveGateway id="Split"/>
                <bpmn:userTask id="Review" camunda:assignee="user:1"/><bpmn:endEvent id="End"/>
                <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Split"/>
                <bpmn:sequenceFlow id="f2" sourceRef="Split" targetRef="Review"/>
                <bpmn:sequenceFlow id="f3" sourceRef="Split" targetRef="Review"/>
                <bpmn:sequenceFlow id="f4" sourceRef="Review" targetRef="End"/>
              </bpmn:process>
            </bpmn:definitions>
            """;

        BpmnParser.Validate(BpmnParser.Parse(bpmn))
            .Should().Contain(error => error.Contains("包容分支不能同时进入"));
    }

    [Fact]
    public void Parser_allows_inclusive_branches_with_explicit_inclusive_join()
    {
        const string bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <bpmn:process id="inclusive-join">
                <bpmn:startEvent id="Start"/><bpmn:inclusiveGateway id="Split"/>
                <bpmn:userTask id="A" camunda:assignee="user:1"/><bpmn:userTask id="B" camunda:assignee="user:1"/>
                <bpmn:inclusiveGateway id="Join"/><bpmn:userTask id="Review" camunda:assignee="user:1"/><bpmn:endEvent id="End"/>
                <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Split"/>
                <bpmn:sequenceFlow id="f2" sourceRef="Split" targetRef="A"/><bpmn:sequenceFlow id="f3" sourceRef="Split" targetRef="B"/>
                <bpmn:sequenceFlow id="f4" sourceRef="A" targetRef="Join"/><bpmn:sequenceFlow id="f5" sourceRef="B" targetRef="Join"/>
                <bpmn:sequenceFlow id="f6" sourceRef="Join" targetRef="Review"/><bpmn:sequenceFlow id="f7" sourceRef="Review" targetRef="End"/>
              </bpmn:process>
            </bpmn:definitions>
            """;

        BpmnParser.Validate(BpmnParser.Parse(bpmn)).Should().BeEmpty();
    }

    [Fact]
    public void Parser_rejects_parallel_branches_that_end_without_joining()
    {
        const string bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <bpmn:process id="parallel-no-join">
                <bpmn:startEvent id="Start"/><bpmn:parallelGateway id="Fork"/>
                <bpmn:userTask id="A" camunda:assignee="user:1"/><bpmn:userTask id="B" camunda:assignee="user:1"/>
                <bpmn:endEvent id="EndA"/><bpmn:endEvent id="EndB"/>
                <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Fork"/>
                <bpmn:sequenceFlow id="f2" sourceRef="Fork" targetRef="A"/><bpmn:sequenceFlow id="f3" sourceRef="Fork" targetRef="B"/>
                <bpmn:sequenceFlow id="f4" sourceRef="A" targetRef="EndA"/><bpmn:sequenceFlow id="f5" sourceRef="B" targetRef="EndB"/>
              </bpmn:process>
            </bpmn:definitions>
            """;

        BpmnParser.Validate(BpmnParser.Parse(bpmn))
            .Should().Contain(error => error.Contains("汇聚前到达结束事件"));
    }

    [Fact]
    public void Parser_rejects_inclusive_split_joined_by_parallel_gateway()
    {
        const string bpmn = """
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <bpmn:process id="inclusive-parallel-join">
                <bpmn:startEvent id="Start"/><bpmn:inclusiveGateway id="Split"/>
                <bpmn:userTask id="A" camunda:assignee="user:1"/><bpmn:userTask id="B" camunda:assignee="user:1"/>
                <bpmn:parallelGateway id="Join"/><bpmn:endEvent id="End"/>
                <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Split"/>
                <bpmn:sequenceFlow id="f2" sourceRef="Split" targetRef="A"><bpmn:conditionExpression>${takeA} == true</bpmn:conditionExpression></bpmn:sequenceFlow>
                <bpmn:sequenceFlow id="f3" sourceRef="Split" targetRef="B"/>
                <bpmn:sequenceFlow id="f4" sourceRef="A" targetRef="Join"/><bpmn:sequenceFlow id="f5" sourceRef="B" targetRef="Join"/>
                <bpmn:sequenceFlow id="f6" sourceRef="Join" targetRef="End"/>
              </bpmn:process>
            </bpmn:definitions>
            """;

        BpmnParser.Validate(BpmnParser.Parse(bpmn))
            .Should().Contain(error => error.Contains("包容分支不能同时进入"));
    }

    [Fact]
    public void Reentering_user_task_preserves_previous_execution_history()
    {
        var process = BpmnParser.Parse(UserTaskLoopBpmn());
        var flow = new TestFlow { Context = new() { ["repeat"] = "true" } };
        BpmnEngine.Start(flow, process);

        BpmnEngine.Approve(flow, process, "Task_Review", "1", "第一次");
        flow.Context!["repeat"] = "false";
        BpmnEngine.Approve(flow, process, "Task_Review", "2", "第二次");

        flow.Status.Should().Be("approved");
        flow.BpmnTokens["Task_Review"].History.Should().ContainSingle()
            .Which.Opinion.Should().Be("第一次");
    }

    [Fact]
    public void Applicant_role_condition_matches_any_active_role_in_context()
    {
        var process = BpmnParser.Parse(MultiRoleBpmn());
        var flow = new TestFlow
        {
            Context = new()
            {
                ["applicantRole"] = "employee",
                ["applicantRoles"] = "employee,supervisor"
            }
        };

        BpmnEngine.Start(flow, process);

        flow.CurrentNodeIds.Should().ContainSingle().Which.Should().Be("Task_Supervisor");
    }

    [Fact]
    public void Parser_rejects_reachable_nodes_that_cannot_reach_an_end_event()
    {
        var process = BpmnParser.Parse(AutomaticCycleWithoutEndBpmn());

        BpmnParser.Validate(process).Should().Contain(error => error.Contains("不存在到结束事件的路径"));
    }

    [Fact]
    public void Validator_accepts_project_owner_condition()
    {
        var bpmn = ProjectOwnerConditionBpmn();

        var errors = BpmnValidator.Validate(bpmn);

        errors.Should().BeEmpty();
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

    private static string ExclusiveMergeBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="exclusiveMerge">
    <bpmn:startEvent id="Start"/><bpmn:exclusiveGateway id="Fork"/>
    <bpmn:userTask id="Task_A" camunda:assignee="1"/><bpmn:userTask id="Task_B" camunda:assignee="2"/>
    <bpmn:exclusiveGateway id="Merge"/><bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Fork"/>
    <bpmn:sequenceFlow id="f2" sourceRef="Fork" targetRef="Task_A"><bpmn:conditionExpression>${approved} == "true"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="f3" sourceRef="Fork" targetRef="Task_B"/>
    <bpmn:sequenceFlow id="f4" sourceRef="Task_A" targetRef="Merge"/><bpmn:sequenceFlow id="f5" sourceRef="Task_B" targetRef="Merge"/>
    <bpmn:sequenceFlow id="f6" sourceRef="Merge" targetRef="End"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private static string InclusiveMergeBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="inclusiveMerge">
    <bpmn:startEvent id="Start"/><bpmn:inclusiveGateway id="Fork"/>
    <bpmn:userTask id="Task_A" camunda:assignee="1"/><bpmn:userTask id="Task_B" camunda:assignee="2"/>
    <bpmn:inclusiveGateway id="Merge"/><bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Fork"/>
    <bpmn:sequenceFlow id="f2" sourceRef="Fork" targetRef="Task_A"><bpmn:conditionExpression>${needA} == "true"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="f3" sourceRef="Fork" targetRef="Task_B"><bpmn:conditionExpression>${needB} == "true"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="f4" sourceRef="Task_A" targetRef="Merge"/><bpmn:sequenceFlow id="f5" sourceRef="Task_B" targetRef="Merge"/>
    <bpmn:sequenceFlow id="f6" sourceRef="Merge" targetRef="End"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private static string ProjectOwnerConditionBpmn() => """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="projectOwnerCondition" isExecutable="true">
    <bpmn:startEvent id="Start" />
    <bpmn:exclusiveGateway id="Gateway" />
    <bpmn:userTask id="Task_Owner" name="指定人员审批" camunda:assignee="1001" />
    <bpmn:userTask id="Task_Default" name="默认审批" camunda:assignee="deptManager" />
    <bpmn:endEvent id="End" />
    <bpmn:sequenceFlow id="F1" sourceRef="Start" targetRef="Gateway" />
    <bpmn:sequenceFlow id="F2" sourceRef="Gateway" targetRef="Task_Owner">
      <bpmn:conditionExpression>${isProjectOwner} == "true"</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="F3" sourceRef="Gateway" targetRef="Task_Default" />
    <bpmn:sequenceFlow id="F4" sourceRef="Task_Owner" targetRef="End" />
    <bpmn:sequenceFlow id="F5" sourceRef="Task_Default" targetRef="End" />
  </bpmn:process>
</bpmn:definitions>
""";

    private static string DepartmentNotEqualsBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="departmentNotEquals">
    <bpmn:startEvent id="Start"/><bpmn:exclusiveGateway id="Gateway"/>
    <bpmn:userTask id="Task_Other" camunda:assignee="user:1"/><bpmn:userTask id="Task_Default" camunda:assignee="user:1"/>
    <bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Gateway"/>
    <bpmn:sequenceFlow id="f2" sourceRef="Gateway" targetRef="Task_Other"><bpmn:conditionExpression>${applicantDept} != "技术部"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="f3" sourceRef="Gateway" targetRef="Task_Default"/>
    <bpmn:sequenceFlow id="f4" sourceRef="Task_Other" targetRef="End"/><bpmn:sequenceFlow id="f5" sourceRef="Task_Default" targetRef="End"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private static string InclusiveConditionalBypassBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="inclusiveBypass">
    <bpmn:startEvent id="Start"/><bpmn:inclusiveGateway id="Fork"/>
    <bpmn:userTask id="Task_A" camunda:assignee="user:1"/><bpmn:userTask id="Task_B" camunda:assignee="user:2"/>
    <bpmn:exclusiveGateway id="Route_B"/><bpmn:inclusiveGateway id="Join"/>
    <bpmn:endEvent id="End_Join"/><bpmn:endEvent id="End_Bypass"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Fork"/>
    <bpmn:sequenceFlow id="f2" sourceRef="Fork" targetRef="Task_A"/><bpmn:sequenceFlow id="f3" sourceRef="Fork" targetRef="Task_B"/>
    <bpmn:sequenceFlow id="f4" sourceRef="Task_A" targetRef="Join"/><bpmn:sequenceFlow id="f5" sourceRef="Task_B" targetRef="Route_B"/>
    <bpmn:sequenceFlow id="f6" sourceRef="Route_B" targetRef="Join"><bpmn:conditionExpression>${joinB} == "true"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="f7" sourceRef="Route_B" targetRef="End_Bypass"/>
    <bpmn:sequenceFlow id="f8" sourceRef="Join" targetRef="End_Join"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private static string DeadEndParallelGatewayBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="deadEndParallel">
    <bpmn:startEvent id="Start"/><bpmn:parallelGateway id="DeadEnd"/><bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="DeadEnd"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private static string UserTaskLoopBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="userLoop">
    <bpmn:startEvent id="Start"/><bpmn:userTask id="Task_Review" camunda:assignee="user:1"/><bpmn:exclusiveGateway id="Route"/><bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Task_Review"/><bpmn:sequenceFlow id="f2" sourceRef="Task_Review" targetRef="Route"/>
    <bpmn:sequenceFlow id="f3" sourceRef="Route" targetRef="Task_Review"><bpmn:conditionExpression>${repeat} == "true"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="f4" sourceRef="Route" targetRef="End"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private static string MultiRoleBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="multiRole">
    <bpmn:startEvent id="Start"/><bpmn:exclusiveGateway id="Route"/>
    <bpmn:userTask id="Task_Supervisor" camunda:assignee="user:1"/><bpmn:userTask id="Task_Default" camunda:assignee="user:1"/><bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Route"/>
    <bpmn:sequenceFlow id="f2" sourceRef="Route" targetRef="Task_Supervisor"><bpmn:conditionExpression>${applicantRole} == "supervisor"</bpmn:conditionExpression></bpmn:sequenceFlow>
    <bpmn:sequenceFlow id="f3" sourceRef="Route" targetRef="Task_Default"/>
    <bpmn:sequenceFlow id="f4" sourceRef="Task_Supervisor" targetRef="End"/><bpmn:sequenceFlow id="f5" sourceRef="Task_Default" targetRef="End"/>
  </bpmn:process>
</bpmn:definitions>
""";

    private static string AutomaticCycleWithoutEndBpmn() => """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:process id="automaticCycle">
    <bpmn:startEvent id="Start"/><bpmn:serviceTask id="Service_A"/><bpmn:serviceTask id="Service_B"/><bpmn:endEvent id="End"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Start" targetRef="Service_A"/><bpmn:sequenceFlow id="f2" sourceRef="Service_A" targetRef="Service_B"/><bpmn:sequenceFlow id="f3" sourceRef="Service_B" targetRef="Service_A"/>
  </bpmn:process>
</bpmn:definitions>
""";

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
        public Dictionary<string, string>? Context { get; set; }
    }
}
