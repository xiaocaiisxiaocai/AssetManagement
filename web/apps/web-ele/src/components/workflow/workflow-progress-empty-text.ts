export interface WorkflowProgressEmptyState {
  directTransfer?: boolean;
  source?: 'asset' | 'material';
  status?: null | string;
}

export function getWorkflowProgressEmptyText({
  directTransfer = false,
  source,
  status,
}: WorkflowProgressEmptyState) {
  if (status === 'receiving') {
    return '待接收人确认，保管人尚未变更';
  }
  if (status === 'approved') {
    if (source === 'material' && directTransfer) {
      return '接收人已确认，无需审批';
    }
    return '无需人工审批，申请已自动通过';
  }
  if (status === 'withdrawn') return '申请已撤回，暂无可展示的审批节点';
  if (status === 'rejected') return '申请已驳回，暂无可展示的审批节点';
  return '暂无流转记录';
}
