import { requestClient } from '#/api/request';

interface ApiResult<T> {
  code: number;
  data: T;
  message: string;
}

// BPMN Token 状态
export enum BpmnTokenStatus {
  Active = 0,
  Completed = 1,
  Skipped = 2,
  Waiting = 3,
}

// BPMN Token
export interface BpmnToken {
  nodeId: string;
  nodeName: string;
  status: BpmnTokenStatus;
  createdAt?: string;
  completedAt?: string;
  signStates?: Record<string, boolean> | null;
  addedSigners?: Record<string, number> | null;
}

// 工作流定义（BPMN 模式）
export interface WorkflowItem {
  bizType: string;
  bizTypeLabel: string;
  id: number;
  isActive: boolean;
  name: string;
  bpmnXml?: string | null; // BPMN 2.0 XML
  bpmnStatus: 'configured' | 'empty' | 'invalid';
  bpmnValidationErrors: string[];
}

export interface SaveWorkflowPayload {
  bizType: string;
  bpmnXml?: null | string;
  name: string;
}

// 审批流程实例
export interface ApprovalFlow {
  actionableNodeIds: string[]; // 当前用户可操作的活跃节点
  applicant: string;
  applicantDept?: null | string;
  applyTime: string;
  assetId: number;
  assetName: string;
  assetNo: string;
  bizType: string;
  confirmedAt?: null | string;
  currentNodeIds: string[]; // BPMN: 当前活跃的节点 ID 列表
  bpmnTokens: Record<string, BpmnToken>; // BPMN: Token 状态字典
  deadline: string;
  flowNo: string;
  id: number;
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
  nodeId?: string; // BPMN: 待办审批应始终显式指定可操作节点 ID
  opinion: string;
}

export interface RejectPayload {
  nodeId?: string; // BPMN: 指定要驳回的节点 ID（可选）
  reason: string;
}

export interface AddSignPayload {
  nodeId?: string;
  who: string;
}

async function unwrap<T>(request: Promise<ApiResult<T>>) {
  const result = await request;
  return result.data;
}

export const getWorkflowsApi = () =>
  unwrap(requestClient.get<ApiResult<WorkflowItem[]>>('/workflows'));

export const getWorkflowApi = (id: number) =>
  unwrap(requestClient.get<ApiResult<WorkflowItem>>(`/workflows/${id}`));

export const createWorkflowApi = (data: SaveWorkflowPayload) =>
  unwrap(requestClient.post<ApiResult<WorkflowItem>>('/workflows', data));

export const saveWorkflowApi = (id: number, data: SaveWorkflowPayload) =>
  unwrap(requestClient.put<ApiResult<WorkflowItem>>(`/workflows/${id}`, data));

export const setWorkflowStatusApi = (id: number, isActive: boolean) =>
  unwrap(
    requestClient.post<ApiResult<WorkflowItem>>(`/workflows/${id}/status`, {
      isActive,
    }),
  );

export const deleteWorkflowApi = (id: number) =>
  unwrap(requestClient.delete<ApiResult<boolean>>(`/workflows/${id}`));

export const startApprovalApi = (data: StartApprovalPayload) =>
  unwrap(requestClient.post<ApiResult<ApprovalFlow>>('/approvals', data));

export const getPendingApprovalsApi = () =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/pending'));

export const getMineApprovalsApi = () =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/mine'));

// 待接收确认:已审批通过、尚未确认接收归还的借用单
export const getPendingReturnsApi = () =>
  unwrap(
    requestClient.get<ApiResult<ApprovalFlow[]>>('/approvals/pending-return'),
  );

export const getFlowDetailApi = (id: number) =>
  unwrap(requestClient.get<ApiResult<ApprovalFlow>>(`/approvals/${id}`));

export const approveFlowApi = (id: number, data: ApprovalActionPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/approve`,
      data,
    ),
  );

export const rejectFlowApi = (id: number, data: RejectPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/reject`,
      data,
    ),
  );

export const withdrawApprovalApi = (id: number) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/withdraw`,
      {},
    ),
  );

export const addSignFlowApi = (id: number, data: AddSignPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/add-sign`,
      data,
    ),
  );

export const cancelAddSignFlowApi = (id: number, data: AddSignPayload) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/cancel-add-sign`,
      data,
    ),
  );

export const confirmReturnApi = (id: number) =>
  unwrap(
    requestClient.post<ApiResult<ApprovalFlow>>(
      `/approvals/${id}/confirm-return`,
      {},
    ),
  );
