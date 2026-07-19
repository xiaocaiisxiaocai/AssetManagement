import type { MaterialFlowItem } from '#/api/material';
import type { ApprovalFlow } from '#/api/workflow';

import { describe, expect, it } from 'vitest';

import {
  canWithdrawApproval,
  findApprovalWorkItemIndex,
  mergeApprovalWorkItems,
  normalizeAssetApproval,
  normalizeMaterialFlow,
} from './approval-work-items';

const assetFlow: ApprovalFlow = {
  actionableNodeIds: ['Task_manager'],
  applicant: '张三',
  applicantDept: '研发一部',
  applyTime: '2026-07-08T09:00:00Z',
  assetId: 1,
  assetName: '示波器',
  assetNo: 'A-001',
  bizType: 'borrow',
  currentNodeIds: ['Task_manager'],
  bpmnTokens: {
    Task_manager: {
      nodeId: 'Task_manager',
      nodeName: '部门经理审批',
      status: 0,
    },
  },
  deadline: '2026-07-10T09:00:00Z',
  flowNo: 'AF-001',
  id: 1,
  reason: '项目测试',
  status: 'pending',
};

const materialFlow: MaterialFlowItem = {
  actionableNodeIds: ['Task_owner'],
  applicant: '李四',
  applicantDept: '测试部',
  applyTime: '2026-07-08T10:00:00Z',
  currentNodeIds: ['Task_owner'],
  bpmnTokens: {
    Task_owner: {
      nodeId: 'Task_owner',
      nodeName: '项目负责人审批',
      status: 0,
    },
  },
  directTransfer: false,
  flowNo: 'MF-001',
  id: 2,
  materialId: 3,
  materialName: '测试料件',
  materialNo: 'M-001',
  reason: '转给接收人验证',
  status: 'pending',
  transferee: '王五',
  transfereeDept: '制造部',
};

describe('审批工作项适配', () => {
  it('把资产审批转换成统一工作项', () => {
    expect(normalizeAssetApproval(assetFlow)).toMatchObject({
      id: 1,
      key: 'asset-1',
      source: 'asset',
      typeLabel: '借用',
      objectNo: 'A-001',
      objectName: '示波器',
      participant: '张三',
      currentNodeLabel: '部门经理审批',
    });
  });

  it('把测试料件流转转换成统一工作项', () => {
    expect(normalizeMaterialFlow(materialFlow)).toMatchObject({
      id: 2,
      key: 'material-2',
      source: 'material',
      typeLabel: '测试料件流转',
      objectNo: 'M-001',
      objectName: '测试料件',
      participant: '王五',
      currentNodeLabel: '项目负责人审批',
    });
  });

  it('并行流程展示全部当前节点而不是只看当前用户可操作节点', () => {
    const flow: ApprovalFlow = {
      ...assetFlow,
      actionableNodeIds: ['Task_manager'],
      currentNodeIds: ['Task_manager', 'Task_admin'],
      bpmnTokens: {
        ...assetFlow.bpmnTokens,
        Task_admin: {
          nodeId: 'Task_admin',
          nodeName: '系统管理员审批',
          status: 0,
        },
      },
    };

    expect(normalizeAssetApproval(flow)).toMatchObject({
      actionableNodeIds: ['Task_manager'],
      currentNodeLabel: '2 个并行节点',
    });
  });

  it('测试料件并行流转展示全部当前节点', () => {
    const flow: MaterialFlowItem = {
      ...materialFlow,
      actionableNodeIds: ['Task_owner'],
      currentNodeIds: ['Task_owner', 'Task_admin'],
      bpmnTokens: {
        ...materialFlow.bpmnTokens,
        Task_admin: {
          nodeId: 'Task_admin',
          nodeName: '系统管理员审批',
          status: 0,
        },
      },
    };

    expect(normalizeMaterialFlow(flow)).toMatchObject({
      actionableNodeIds: ['Task_owner'],
      currentNodeLabel: '项目负责人审批等 2 个并行节点',
    });
  });

  it('申请人不可审批时仍展示流程当前节点', () => {
    const flow: ApprovalFlow = {
      ...assetFlow,
      actionableNodeIds: [],
    };

    expect(normalizeAssetApproval(flow)).toMatchObject({
      actionableNodeIds: [],
      currentNodeLabel: '部门经理审批',
      status: 'pending',
    });
  });

  it('合并后按申请时间倒序展示', () => {
    const result = mergeApprovalWorkItems([assetFlow], [materialFlow]);

    expect(result.map((item) => item.key)).toEqual(['material-2', 'asset-1']);
  });

  it('只有审批中的申请允许撤回', () => {
    expect(canWithdrawApproval({ status: 'pending' })).toBe(true);
    expect(canWithdrawApproval({ status: 'approved' })).toBe(false);
    expect(canWithdrawApproval({ status: 'rejected' })).toBe(false);
    expect(canWithdrawApproval({ status: 'withdrawn' })).toBe(false);
  });

  it('按来源和编号共同定位审批项，避免跨业务同号误更新', () => {
    const items = [
      { id: 7, source: 'material' as const },
      { id: 7, source: 'asset' as const },
    ];
    expect(findApprovalWorkItemIndex(items, 'asset', 7)).toBe(1);
  });
});
