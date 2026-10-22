import type { TestProjectItem } from '#/api/test-project';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  deleteTestProjectApi,
  purgeTestProjectApi,
  reopenTestProjectApi,
  restoreTestProjectApi,
} from '#/api/test-project';

export function bindProjectCatalogDeleteActions(deps: {
  loadData: () => Promise<void>;
}) {
  const { loadData } = deps;

  async function remove(row: TestProjectItem) {
    try {
      await ElMessageBox.confirm(`确认删除项目「${row.name}」？`, '删除确认', {
        type: 'warning',
      });
    } catch {
      return;
    }
    try {
      await deleteTestProjectApi(row.id);
      ElMessage.success('已删除');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function restore(row: TestProjectItem) {
    try {
      await ElMessageBox.confirm(
        `确认撤销删除项目「${row.name}」？`,
        '撤销删除',
        {
          type: 'warning',
        },
      );
    } catch {
      return;
    }
    try {
      await restoreTestProjectApi(row.id);
      ElMessage.success('已恢复');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function purge(row: TestProjectItem) {
    try {
      await ElMessageBox.confirm(
        `彻底删除项目「${row.name}」后不可恢复，确认继续？`,
        '彻底删除确认',
        { type: 'warning' },
      );
    } catch {
      return;
    }
    try {
      await purgeTestProjectApi(row.id);
      ElMessage.success('已彻底删除');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  async function reopen(row: TestProjectItem) {
    try {
      await ElMessageBox.confirm(
        `重新打开后，项目「${row.name}」会回到测试中，可以继续修正阶段、报告和料件。确认重新打开？`,
        '重新打开',
        { type: 'warning', confirmButtonText: '重新打开', cancelButtonText: '取消' },
      );
    } catch {
      return;
    }
    try {
      await reopenTestProjectApi(row.id, row.rowVersion);
      ElMessage.success('已重新打开');
      await loadData();
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  return { purge, remove, reopen, restore };
}
