import type { ProjectFlowListKind } from './project-flow-actions';

import type { TestProjectItem } from '#/api/test-project';

import { ElMessage } from 'element-plus';

import { getMaterialFlowApi } from '#/api/material';
import { listTestProjectsPageApi } from '#/api/test-project';

export const receiptProjectUnavailableMessage =
  '无法打开该项目，请联系申请人撤回后重新发起';

const receiptPageSize = 200;

export function receiptNotificationFlowId(query: {
  flowId?: unknown;
  intent?: unknown;
  source?: unknown;
}) {
  if (queryText(query.intent) !== 'receive') return 0;
  if (queryText(query.source) !== 'material') return 0;
  const flowId = Number(queryText(query.flowId) || 0);
  return Number.isInteger(flowId) && flowId > 0 ? flowId : 0;
}

export async function findProjectForReceipt(
  projectId: number,
  loadedProjects: TestProjectItem[],
  loadPage: (
    page: number,
  ) => Promise<{ items: TestProjectItem[]; total: number }>,
) {
  const loaded = loadedProjects.find((project) => project.id === projectId);
  if (loaded) return loaded;
  let seen = 0;
  for (let page = 1; page <= 50; page += 1) {
    const result = await loadPage(page);
    seen += result.items.length;
    const found = result.items.find((project) => project.id === projectId);
    if (found) return found;
    if (result.items.length === 0 || seen >= result.total) return null;
  }
  return null;
}

export function bindProjectReceiptNavigation(
  page: Record<string, any>,
  deps: {
    openFollowups: (
      row: TestProjectItem,
      options?: { flowTab?: ProjectFlowListKind },
    ) => Promise<void>;
  },
) {
  const { route, router, projects } = page;
  let opening = false;

  async function openReceiptNotification() {
    const flowId = receiptNotificationFlowId(route.query ?? {});
    if (!flowId || opening) return;
    opening = true;
    try {
      const flow = await getMaterialFlowApi(flowId);
      const project = await findProjectForReceipt(
        flow.projectId,
        projects.value ?? [],
        (pageNumber) =>
          listTestProjectsPageApi({
            deleteStatus: 'all',
            page: pageNumber,
            pageSize: receiptPageSize,
          }),
      );
      if (!project) {
        ElMessage.warning(receiptProjectUnavailableMessage);
        return;
      }
      await deps.openFollowups(project, { flowTab: 'receiving' });
    } catch {
      ElMessage.warning(receiptProjectUnavailableMessage);
    } finally {
      opening = false;
      clearReceiptQuery();
    }
  }

  function clearReceiptQuery() {
    if (!route.query?.flowId && route.query?.intent !== 'receive') return;
    const nextQuery = { ...route.query };
    delete nextQuery.flowId;
    delete nextQuery.source;
    delete nextQuery.intent;
    router.replace({ path: route.path, query: nextQuery });
  }

  return { openReceiptNotification };
}

function queryText(value: unknown) {
  const text = Array.isArray(value) ? value[0] : value;
  return typeof text === 'string' ? text : '';
}
