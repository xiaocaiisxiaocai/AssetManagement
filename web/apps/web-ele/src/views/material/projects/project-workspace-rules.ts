import type { MaterialFlowItem } from '#/api/material';
import type { TestProjectItem } from '#/api/test-project';

export function canWithdrawMaterialFlow(flow: MaterialFlowItem) {
  return flow.status === 'pending' && flow.canWithdraw === true;
}

export function projectFollowUpStatusMeta(
  project: Pick<TestProjectItem, 'closedDate' | 'followUpStatus' | 'isDeleted'>,
) {
  if (project.isDeleted) {
    return { label: '已删除', type: 'info' as const };
  }
  if (project.closedDate) {
    return { label: '已结案', type: 'info' as const };
  }
  const statuses = {
    due: { label: '今日到期', type: 'warning' as const },
    overdue: { label: '已超期', type: 'danger' as const },
    upcoming: { label: '未到期', type: 'success' as const },
  };
  return (
    statuses[project.followUpStatus as keyof typeof statuses] ?? {
      label: '未到期',
      type: 'success' as const,
    }
  );
}
