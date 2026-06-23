# BPMN 工作流迁移 - 当前进度与待办事项

**日期**: 2026-06-22
**状态**: 核心代码已完成 70%，需要继续修复编译错误

---

## ✅ 已完成

### 1. 前端 BPMN 设计器（完成）
- ✅ 创建 `web/apps/web-ele/src/views/admin/workflows/bpmn-modeler.vue`
- ✅ 使用 CDN 加载 bpmn-js（规避 pnpm 内存问题）
- ✅ 支持保存/加载/下载 BPMN XML
- ✅ 基础工具栏和画布

### 2. 后端 Domain 层（完成）
- ✅ `BpmnModels.cs` - BPMN 数据模型
- ✅ `BpmnParser.cs` - 完整的 BPMN 2.0 XML 解析器
- ✅ `BpmnEngine.cs` - Token 驱动的执行引擎
- ✅ 支持排他网关、并行网关、包容网关
- ✅ 删除旧的 WorkflowEngine 和 WorkflowModels

### 3. 实体更新（完成）
- ✅ `Workflow` 实体：添加 `BpmnXml`，移除 `Nodes`
- ✅ `ApprovalFlow` 实体：改为 `CurrentNodeIds` + `BpmnTokens`，实现 `IBpmnFlowInstance`

### 4. Application 层（完成）
- ✅ 更新所有 DTO 使用 BPMN 字段
- ✅ 更新请求/响应模型

### 5. Infrastructure 层（部分完成）
- ✅ 完全重写 `WorkflowService.cs` 使用 BPMN 引擎
- ✅ 更新 `WorkflowConfiguration.cs`
- ✅ 更新 `ApprovalFlowConfiguration.cs` 支持 JSON 序列化

---

## ❌ 待修复的编译错误

### 错误类别 1: DbSeeder 使用旧结构（约 20 个错误）
**位置**: `backend/src/AssetManagement.Infrastructure/Persistence/Seed/DbSeeder.cs`

**问题**: 种子数据仍在使用旧的 `WorkflowNode[]` 结构

**解决方案**: 需要转换为 BPMN XML。示例：

```csharp
// 旧代码（行 190-196）
Nodes = new List<WorkflowNode>
{
    new() { Id = "1", Name = "发起", Type = NodeType.Start },
    new() { Id = "2", Name = "直属主管审批", Type = NodeType.Approval, ApproverType = ApproverType.Supervisor },
    // ...
}

// 新代码
BpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
                  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
                  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
                  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
                  xmlns:camunda=""http://camunda.org/schema/1.0/bpmn""
                  id=""Definitions_borrow"">
  <bpmn:process id=""Process_borrow"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1"" name=""发起借用申请"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:userTask id=""Task_supervisor"" name=""直属主管审批"" camunda:assignee=""supervisor"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:exclusiveGateway id=""Gateway_amount"" name=""金额判断"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
      <bpmn:outgoing>Flow_high</bpmn:outgoing>
      <bpmn:outgoing>Flow_low</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_dept_manager"" name=""部门经理审批"" camunda:assignee=""deptManager"">
      <bpmn:incoming>Flow_high</bpmn:incoming>
      <bpmn:outgoing>Flow_3</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""EndEvent_1"" name=""流程结束"">
      <bpmn:incoming>Flow_3</bpmn:incoming>
      <bpmn:incoming>Flow_low</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""StartEvent_1"" targetRef=""Task_supervisor"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_supervisor"" targetRef=""Gateway_amount"" />
    <bpmn:sequenceFlow id=""Flow_high"" name=""金额>5000"" sourceRef=""Gateway_amount"" targetRef=""Task_dept_manager"">
      <bpmn:conditionExpression>${amount} > 5000</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""Flow_low"" name=""金额<=5000"" sourceRef=""Gateway_amount"" targetRef=""EndEvent_1"">
      <bpmn:conditionExpression>${amount} <= 5000</bpmn:conditionExpression>
    </bpmn:sequenceFlow>
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Task_dept_manager"" targetRef=""EndEvent_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_borrow"">
      <bpmndi:BPMNShape id=""StartEvent_1_di"" bpmnElement=""StartEvent_1"">
        <dc:Bounds x=""152"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_supervisor_di"" bpmnElement=""Task_supervisor"">
        <dc:Bounds x=""240"" y=""80"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_amount_di"" bpmnElement=""Gateway_amount"" isMarkerVisible=""true"">
        <dc:Bounds x=""395"" y=""95"" width=""50"" height=""50"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_dept_manager_di"" bpmnElement=""Task_dept_manager"">
        <dc:Bounds x=""500"" y=""80"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""EndEvent_1_di"" bpmnElement=""EndEvent_1"">
        <dc:Bounds x=""652"" y=""102"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1"">
        <di:waypoint x=""188"" y=""120"" />
        <di:waypoint x=""240"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2"">
        <di:waypoint x=""340"" y=""120"" />
        <di:waypoint x=""395"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_high_di"" bpmnElement=""Flow_high"">
        <di:waypoint x=""445"" y=""120"" />
        <di:waypoint x=""500"" y=""120"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_low_di"" bpmnElement=""Flow_low"">
        <di:waypoint x=""420"" y=""145"" />
        <di:waypoint x=""420"" y=""200"" />
        <di:waypoint x=""670"" y=""200"" />
        <di:waypoint x=""670"" y=""138"" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3"">
        <di:waypoint x=""600"" y=""120"" />
        <di:waypoint x=""652"" y=""120"" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>"
```

需要为三个流程创建 BPMN XML：
1. 借用流程（borrow）
2. 转让流程（transfer）
3. 归还流程（return）

### 错误类别 2: WorkflowService 引用不存在的字段（约 10 个错误）

**问题 1**: Asset 实体不存在 `Custodian` 和 `CustodianDept` 字段
**位置**: `WorkflowService.cs:263-264`
**解决方案**: 检查 Asset 实体的实际字段名，可能是 `CustodianId`/`CustodianName` 等

**问题 2**: User 实体不存在 `Roles` 导航属性
**位置**: 多处
**解决方案**: 检查 User 实体的角色关联方式，可能需要通过中间表查询

**问题 3**: AppDbContext 不存在 `ApprovalRecords`
**位置**: `WorkflowService.cs:401`
**解决方案**: 
- 如果有审批记录表，需要在 DbContext 中添加 `DbSet<ApprovalRecord>`
- 如果没有，可能需要创建该实体

---

## 📋 剩余待办任务

### 高优先级（阻塞编译）
1. **修复 DbSeeder** - 转换三个流程为 BPMN XML
2. **修复 WorkflowService** - 检查并修正实体字段引用
3. **创建数据库迁移** - `dotnet ef migrations add MigrateToBpmnWorkflow`
4. **运行迁移** - 确保数据库结构正确

### 中优先级（功能完善）
5. **前端路由更新** - 替换 `workflows/index.vue` 为 `bpmn-modeler.vue`
6. **前端 API 更新** - 确保调用新的接口格式
7. **属性面板** - 实现 BPMN 节点属性编辑（审批人配置）

### 低优先级（测试与文档）
8. **单元测试** - 为 BpmnParser 和 BpmnEngine 编写测试
9. **集成测试** - 更新现有测试适配 BPMN
10. **更新文档** - CLAUDE.md 和设计文档

---

## 🛠 后续实施建议

### 方案 A：继续手动修复（预计 1-2 小时）
1. 先检查 Asset、User、ApprovalRecord 等实体的实际结构
2. 修复 WorkflowService 中的字段引用
3. 创建 BPMN XML 种子数据
4. 编译通过后创建迁移
5. 测试基本流程

### 方案 B：分阶段实施（推荐）
**阶段 1** - 编译通过（30分钟）
- 临时注释掉 DbSeeder 中的 Workflow 初始化
- 修复 WorkflowService 字段引用
- 创建空的 BPMN XML 占位符
- 确保编译成功

**阶段 2** - 前端集成（1小时）
- 更新前端路由
- 测试设计器保存/加载
- 手动创建一个简单的 BPMN 流程

**阶段 3** - 完善种子数据（1小时）
- 创建完整的三个 BPMN XML
- 更新数据库
- 端到端测试

---

## 💡 关键设计决策记录

1. **使用 CDN 而非 npm** - 避开 pnpm 在 Windows 上的内存问题
2. **接口解耦** - `IBpmnFlowInstance` 避免 Domain 层依赖 Entities
3. **保持 BPMN 标准** - 使用 Camunda 扩展属性存储审批人配置
4. **不兼容旧流程** - 全面切换，简化迁移逻辑

---

蔡工，当前核心代码架构已完成，但还有约 30 个编译错误需要修复。建议采用方案 B 分阶段实施，先让代码编译通过再逐步完善功能。

是否需要我继续修复剩余错误？还是你想先review一下现有代码？
