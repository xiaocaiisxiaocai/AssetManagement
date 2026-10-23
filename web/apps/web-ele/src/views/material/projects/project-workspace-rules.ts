import type { MaterialFlowItem } from '#/api/material';
import type { TestProjectItem } from '#/api/test-project';

const SUPPORTED_PROJECT_PROGRESS_CODES = new Set([
  'planning',
  'testing',
  'landing',
  'closed',
]);

export function isSupportedProjectProgress(value: string) {
  return SUPPORTED_PROJECT_PROGRESS_CODES.has(value.trim().toLowerCase());
}

export function canReleaseMaterialFlow(flow: MaterialFlowItem) {
  return flow.status === 'pending' && flow.canReleaseStuck === true;
}

export function canWithdrawMaterialFlow(flow: MaterialFlowItem) {
  return (
    flow.status === 'pending' &&
    flow.canWithdraw === true &&
    flow.canReleaseStuck !== true
  );
}

export async function refreshProjectAfterMaterialChange(
  loadMaterials: () => Promise<unknown>,
  loadFlows: () => Promise<unknown>,
  loadProjects: () => Promise<unknown>,
) {
  await Promise.all([loadMaterials(), loadFlows(), loadProjects()]);
}

export function allowedProgressCodes(
  current: string,
  testingIncomplete = false,
) {
  switch (current.trim().toLowerCase()) {
    case '':
    case 'planning': {
      return ['planning', 'testing'];
    }
    case 'closed': {
      return ['closed'];
    }
    case 'landing': {
      return testingIncomplete ? ['testing'] : ['landing', 'closed'];
    }
    case 'testing': {
      return testingIncomplete ? ['testing'] : ['testing', 'landing', 'closed'];
    }
    default: {
      return ['planning', 'testing'];
    }
  }
}

export function canUpdateProjectProgress(
  project: Pick<TestProjectItem, 'closedDate' | 'isDeleted' | 'ownerId'>,
  currentUserId: number,
  canManage = false,
) {
  return (
    !project.isDeleted &&
    !project.closedDate &&
    currentUserId > 0 &&
    (Number(project.ownerId) === currentUserId || canManage)
  );
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
