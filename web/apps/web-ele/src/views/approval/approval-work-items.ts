import type { MaterialFlowItem } from '#/api/material';
import type { ApprovalFlow } from '#/api/workflow';

export type ApprovalWorkItemSource = 'asset' | 'material';

export interface ApprovalWorkItem {
  actionableNodeIds: string[];
  applicant: string;
  applicantDept?: null | string;
  applyTime: string;
  bizType: string;
  currentNodeLabel: string;
  flowNo: string;
  id: number;
  key: string;
  objectName: string;
  objectNo: string;
  participant?: null | string;
  raw: ApprovalFlow | MaterialFlowItem;
  reason?: null | string;
  source: ApprovalWorkItemSource;
  sourceLabel: string;
  status: string;
  transferee?: null | string;
  transfereeDept?: null | string;
  typeLabel: string;
}

const assetBizTypeLabels: Record<string, string> = {
  borrow: '借用',
  return: '归还',
  transfer: '转让',
};

export function normalizeAssetApproval(flow: ApprovalFlow): ApprovalWorkItem {
  return {
    actionableNodeIds: flow.actionableNodeIds,
    applicant: flow.applicant,
    applicantDept: flow.applicantDept,
    applyTime: flow.applyTime,
    bizType: flow.bizType,
    currentNodeLabel: currentAssetNodeLabel(flow),
    flowNo: flow.flowNo,
    id: flow.id,
    key: `asset-${flow.id}`,
    objectName: flow.assetName,
    objectNo: flow.assetNo,
    participant: flow.bizType === 'transfer' ? flow.transferee : flow.applicant,
    raw: flow,
    reason: flow.reason,
    source: 'asset',
    sourceLabel: '固定资产',
    status: flow.status,
    transferee: flow.transferee,
    transfereeDept: flow.transfereeDept,
    typeLabel: assetBizTypeLabels[flow.bizType] ?? flow.bizType,
  };
}

export function normalizeMaterialFlow(
  flow: MaterialFlowItem,
): ApprovalWorkItem {
  return {
    actionableNodeIds: flow.actionableNodeIds,
    applicant: flow.applicant,
    applicantDept: flow.applicantDept,
    applyTime: flow.applyTime,
    bizType: 'material_transfer',
    currentNodeLabel: currentMaterialNodeLabel(flow),
    flowNo: flow.flowNo,
    id: flow.id,
    key: `material-${flow.id}`,
    objectName: flow.materialName,
    objectNo: flow.materialNo,
    participant: flow.transferee,
    raw: flow,
    reason: flow.reason,
    source: 'material',
    sourceLabel: '测试料件',
    status: flow.status,
    transferee: flow.transferee,
    transfereeDept: flow.transfereeDept,
    typeLabel: '测试料件流转',
  };
}

export function mergeApprovalWorkItems(
  assetFlows: ApprovalFlow[],
  materialFlows: MaterialFlowItem[],
) {
  return [
    ...assetFlows.map(normalizeAssetApproval),
    ...materialFlows.map(normalizeMaterialFlow),
  ].sort((left, right) => {
    const rightTime = new Date(right.applyTime).getTime();
    const leftTime = new Date(left.applyTime).getTime();
    if (rightTime !== leftTime) return rightTime - leftTime;
    return right.id - left.id;
  });
}

export function canWithdrawApproval(item: Pick<ApprovalWorkItem, 'status'>) {
  return item.status === 'pending';
}

function currentAssetNodeLabel(flow: ApprovalFlow) {
  if (flow.currentNodeIds.length === 0) return '-';
  if (flow.currentNodeIds.length > 1)
    return `${flow.currentNodeIds.length} 个并行节点`;

  const nodeId = flow.currentNodeIds[0];
  if (!nodeId) return '-';
  return flow.bpmnTokens[nodeId]?.nodeName || nodeId || '-';
}

function currentMaterialNodeLabel(flow: MaterialFlowItem) {
  if (flow.currentNodeIds.length === 0) return '-';
  const nodeId = flow.currentNodeIds[0];
  if (!nodeId) return '-';
  const nodeName = flow.bpmnTokens[nodeId]?.nodeName || nodeId;
  if (flow.currentNodeIds.length > 1)
    return `${nodeName}等 ${flow.currentNodeIds.length} 个并行节点`;
  return nodeName;
}
