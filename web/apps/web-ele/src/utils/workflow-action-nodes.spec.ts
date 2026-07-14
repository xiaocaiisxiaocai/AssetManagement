import { describe, expect, it } from 'vitest';

import {
  findActionableWorkflowNode,
  formatActionableWorkflowNode,
  formatWorkflowNode,
  getActionableWorkflowNodes,
  getDirectActionableWorkflowNode,
} from './workflow-action-nodes';

describe('审批可操作节点', () => {
  it('没有可操作节点时拒绝推断当前活跃节点', () => {
    const flow = {
      actionableNodeIds: [],
      bpmnTokens: {
        Task_other: { nodeName: '其他人的审批节点' },
      },
      currentNodeIds: ['Task_other'],
    };

    expect(getDirectActionableWorkflowNode(flow)).toBeUndefined();
    expect(formatActionableWorkflowNode(flow)).toBe('无可操作节点');
  });

  it('单节点返回明确的节点名称和 ID', () => {
    const flow = {
      actionableNodeIds: ['Task_manager'],
      bpmnTokens: {
        Task_manager: { nodeName: '主管审批' },
      },
    };

    expect(getActionableWorkflowNodes(flow)).toEqual([
      { id: 'Task_manager', name: '主管审批' },
    ]);
    expect(getDirectActionableWorkflowNode(flow)).toEqual({
      id: 'Task_manager',
      name: '主管审批',
    });
    expect(formatActionableWorkflowNode(flow)).toBe('主管审批（Task_manager）');
  });

  it('多节点不隐式选择并要求使用有效节点 ID', () => {
    const flow = {
      actionableNodeIds: ['Task_finance', 'Task_security'],
      bpmnTokens: {
        Task_finance: { nodeName: '财务审批' },
        Task_security: { nodeName: '安全审批' },
      },
    };

    expect(getDirectActionableWorkflowNode(flow)).toBeUndefined();
    expect(findActionableWorkflowNode(flow, 'Task_security')).toEqual({
      id: 'Task_security',
      name: '安全审批',
    });
    expect(findActionableWorkflowNode(flow, 'Task_other')).toBeUndefined();
    expect(formatActionableWorkflowNode(flow)).toBe(
      '财务审批等 2 个可操作节点',
    );
    expect(formatWorkflowNode({ id: 'Task_security', name: '安全审批' })).toBe(
      '安全审批（Task_security）',
    );
  });
});
