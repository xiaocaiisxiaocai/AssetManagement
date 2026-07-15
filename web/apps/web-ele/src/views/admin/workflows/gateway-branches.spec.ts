import { describe, expect, it } from 'vitest';

import {
  gatewaySupportsConditions,
  getBranchLabelDelta,
  getConditionSummary,
  getSuggestedBranchName,
  getGatewayValidationError,
  getTargetNodeTitle,
  isDefaultGatewayBranch,
} from './gateway-branches';

describe('流程设计器网关分支展示', () => {
  it('保留已配置的连线名称', () => {
    expect(
      getSuggestedBranchName({
        gatewayType: 'bpmn:ExclusiveGateway',
        index: 0,
        name: '  项目负责人通道  ',
      }),
    ).toBe('项目负责人通道');
  });

  it('把项目负责人条件转换为可读标签', () => {
    expect(getConditionSummary('${isProjectOwner} == "true"')).toBe(
      '是项目负责人',
    );
    expect(getConditionSummary('${isProjectOwner} == "false"')).toBe(
      '非项目负责人',
    );
  });

  it('把旧流程的无条件出线识别为默认分支', () => {
    const branch = {
      gatewayType: 'bpmn:ExclusiveGateway',
      index: 1,
      targetId: 'Task_manager',
      targetName: '部门负责人审批',
      targetType: 'bpmn:UserTask',
    };

    expect(isDefaultGatewayBranch(branch)).toBe(true);
    expect(getSuggestedBranchName(branch)).toBe('其他情况（默认）');
  });

  it('带条件的显式默认引用不能冒充后端要求的无条件分支', () => {
    expect(
      isDefaultGatewayBranch({
        conditionExpression: '${applicantRole} == "employee"',
        gatewayType: 'bpmn:ExclusiveGateway',
        index: 0,
        isExplicitDefault: true,
      }),
    ).toBe(false);
  });

  it('并行网关使用目标节点生成稳定名称且不误判默认分支', () => {
    const branch = {
      gatewayType: 'bpmn:ParallelGateway',
      index: 0,
      targetId: 'Task_security',
      targetName: '安全评审',
      targetType: 'bpmn:UserTask',
    };

    expect(isDefaultGatewayBranch(branch)).toBe(false);
    expect(getSuggestedBranchName(branch)).toBe('并行至「安全评审」');
  });

  it('目标节点没有名称时显示中文节点类型', () => {
    expect(
      getTargetNodeTitle({
        gatewayType: 'bpmn:ExclusiveGateway',
        index: 0,
        targetId: 'End_1',
        targetType: 'bpmn:EndEvent',
      }),
    ).toBe('流程结束');
  });

  it('断开的连线明确显示未连接', () => {
    expect(
      getTargetNodeTitle({
        gatewayType: 'bpmn:ExclusiveGateway',
        index: 0,
      }),
    ).toBe('未连接节点');
  });

  it('排他和包容网关支持条件，并行网关不支持条件', () => {
    expect(gatewaySupportsConditions('bpmn:ExclusiveGateway')).toBe(true);
    expect(gatewaySupportsConditions('bpmn:InclusiveGateway')).toBe(true);
    expect(gatewaySupportsConditions('bpmn:ParallelGateway')).toBe(false);
  });

  it('把上方分支标签放到最长横线段的上侧', () => {
    expect(
      getBranchLabelDelta(
        [
          { x: 100, y: 100 },
          { x: 100, y: 50 },
          { x: 220, y: 50 },
        ],
        { height: 20, width: 80, x: 90, y: 70 },
        { x: 100, y: 100 },
      ),
    ).toEqual({ x: 30, y: -48 });
  });

  it('把下方分支标签放到最长横线段的下侧', () => {
    expect(
      getBranchLabelDelta(
        [
          { x: 100, y: 100 },
          { x: 100, y: 150 },
          { x: 220, y: 150 },
        ],
        { height: 20, width: 80, x: 90, y: 120 },
        { x: 100, y: 100 },
      ),
    ).toEqual({ x: 30, y: 38 });
  });

  it('保存前拦截没有默认分支的排他网关', () => {
    expect(
      getGatewayValidationError({
        expressions: [
          '${isProjectOwner} == "true"',
          '${isProjectOwner} == "false"',
        ],
        gatewayName: '是否项目负责人',
        gatewayType: 'bpmn:ExclusiveGateway',
      }),
    ).toContain('当前有 0 条默认分支');
  });

  it('保存前拦截配置了条件的并行网关', () => {
    expect(
      getGatewayValidationError({
        expressions: ['${applicantDept} == "技术部"', ''],
        gatewayName: '并行评审',
        gatewayType: 'bpmn:ParallelGateway',
      }),
    ).toContain('不能设置条件');
  });
});
