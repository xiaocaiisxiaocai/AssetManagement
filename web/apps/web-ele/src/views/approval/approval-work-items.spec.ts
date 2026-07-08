import type { MaterialFlowItem } from '#/api/material';
import type { ApprovalFlow } from '#/api/workflow';

import { describe, expect, it } from 'vitest';

import {
  mergeApprovalWorkItems,
  normalizeAssetApproval,
  normalizeMaterialFlow,
} from './approval-work-items';

const assetFlow: ApprovalFlow = {
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
  applicant: '李四',
  applicantDept: '测试部',
  applyTime: '2026-07-08T10:00:00Z',
  currentNodeIds: ['Task_owner'],
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
      currentNodeLabel: '1 个待审批节点',
    });
  });

  it('合并后按申请时间倒序展示', () => {
    const result = mergeApprovalWorkItems([assetFlow], [materialFlow]);

    expect(result.map((item) => item.key)).toEqual(['material-2', 'asset-1']);
  });
});
