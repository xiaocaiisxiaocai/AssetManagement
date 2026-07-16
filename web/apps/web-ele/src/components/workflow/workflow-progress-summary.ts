export type WorkflowTerminalTone = 'danger' | 'info' | 'success';

export interface WorkflowTerminalProgress {
  text: string;
  tone: WorkflowTerminalTone;
}

export function getTerminalProgress(
  status: string,
): WorkflowTerminalProgress | undefined {
  return {
    approved: { text: '审批已完成', tone: 'success' as const },
    rejected: { text: '审批已驳回', tone: 'danger' as const },
    withdrawn: { text: '申请已撤回', tone: 'info' as const },
  }[status];
}
