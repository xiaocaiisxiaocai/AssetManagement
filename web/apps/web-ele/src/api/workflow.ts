import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

export type NodeType = 0 | 1 | 2 | 3 | 4 | 5 | 6;
export type ApproverType = 0 | 1 | 2 | 3 | 4;
export type NodeStatus = 0 | 1 | 2 | 3 | 4;

export interface WorkflowNode {
  approver?: null | string;
  approverType: ApproverType;
  condition?: null | string;
  id: string;
  name: string;
  signers?: null | string[];
  type: NodeType;
  x?: null | number;
  y?: null | number;
}

export interface FlowNode {
  addedSigners?: null | string[];
  approver?: null | string;
  condition?: null | string;
  name: string;
  opinion?: null | string;
  signers?: null | string[];
  signStates?: null | Record<string, boolean>;
  status: NodeStatus;
  time?: null | string;
  type: NodeType;
}

export interface WorkflowItem {
  bizType: string;
  id: number;
  name: string;
  nodes: WorkflowNode[];
}

export interface ApprovalFlow {
  amount: number;
  applicant: string;
  applicantDept?: null | string;
  applyTime: string;
  assetId: number;
  assetName: string;
  assetNo: string;
  bizType: string;
  confirmedAt?: null | string;
  currentNodeIndex: number;
  deadline: string;
  flowNo: string;
  id: number;
  nodes: FlowNode[];
  reason?: null | string;
  returnDate?: null | string;
  status: string;
  transferee?: null | string;
  transfereeDept?: null | string;
}

export interface StartApprovalPayload {
  assetId: number;
  bizType: string;
  reason?: string;
  returnDate?: string;
  transfereeId?: number;
}

export interface ApprovalActionPayload {
  opinion: string;
  signer?: string;
}

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const getWorkflowsApi = () =>
  unwrap(requestClient.get<ApiResult<WorkflowItem[]>>('/workflows'));

export const saveWorkflowApi = (id: number, data: Omit<WorkflowItem, 'id'>) =>
  unwrap(requestClient.put<ApiResult<WorkflowItem>>(`/workflows/${id}`, data));

export const startApprovalApi = (data: StartApprovalPayload) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>('/approvals', data));

export const getPendingApprovalsApi = () =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/pending'));

export const getMineApprovalsApi = () =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/mine'));

// 待入库:全局已审批通过、尚未确认入库的借用单(资产管理员处理,需 asset:edit 权限)
export const getPendingReturnsApi = () =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/pending-return'));

export const approveFlowApi = (id: number, data: ApprovalActionPayload) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>(`/approvals/${id}/approve`, data));

export const rejectFlowApi = (id: number, reason: string) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>(`/approvals/${id}/reject`, { reason }));

export const addSignApi = (id: number, who: string) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>(`/approvals/${id}/add-sign`, { who }));

export const transferSignApi = (id: number, who: string) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>(`/approvals/${id}/transfer-sign`, { who }));

export const confirmReturnApi = (id: number) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>(`/approvals/${id}/confirm-return`, {}));
