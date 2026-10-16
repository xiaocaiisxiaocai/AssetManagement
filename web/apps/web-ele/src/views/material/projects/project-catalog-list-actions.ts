import type { TestProjectItem } from '#/api/test-project';
import type { UserDto, UserOptionDto } from '#/api/user';

import { getDepartmentOptionsApi } from '#/api/base-data';
import {
  exportTestProjectsApi,
  listTestProjectOptionsApi,
  listTestProjectsPageApi,
} from '#/api/test-project';
import { getUserListApi, getUserOptionsPageApi } from '#/api/user';
import { downloadBlob } from '#/utils/download';
import { runHandled } from '#/utils/handled-promise';
import { normalizedPage } from '#/utils/pagination';
import { replaceUserOptions } from '#/utils/user-options';

import { localTodayText } from './followup-date-rules';
import { buildProjectPageQuery } from './project-page-query';

export function bindProjectCatalogListActions(page: Record<string, any>) {
  const {
    hasAccessByCodes,
    currentProject,
    loading,
    projects,
    projectTotal,
    deleteStatus,
    projectQuery,
    projectFilter,
    options,
    users,
    userOptionsLoading,
    userOptionsRequestGuard,
    departments,
    form,
    projectListRequestGuard,
  } = page;

  async function loadData() {
    const requestGeneration = projectListRequestGuard.next();
    loading.value = true;
    try {
      const result = await listTestProjectsPageApi(
        buildProjectPageQuery(
          projectFilter,
          deleteStatus.value,
          projectQuery.page,
          projectQuery.pageSize,
        ),
      );
      if (!projectListRequestGuard.isLatest(requestGeneration)) return;
      const validPage = normalizedPage(
        projectQuery.page,
        result.total,
        projectQuery.pageSize,
      );
      if (projectQuery.page !== validPage) {
        projectQuery.page = validPage;
        await loadData();
        return;
      }
      projects.value = result.items;
      projectTotal.value = result.total;
      if (currentProject.value) {
        const refreshed = projects.value.find(
          (project: TestProjectItem) => project.id === currentProject.value?.id,
        );
        if (refreshed) currentProject.value = refreshed;
      }
    } finally {
      if (projectListRequestGuard.isLatest(requestGeneration))
        loading.value = false;
    }
  }

  function searchProjects() {
    projectQuery.page = 1;
    runHandled(loadData());
  }

  function resetProjectFilter() {
    Object.assign(projectFilter, {
      code: '',
      name: '',
      ownerIds: [],
      progressCodes: [],
      projectTypeCodes: [],
    });
    projectQuery.page = 1;
    runHandled(loadData());
  }

  async function loadOptions() {
    options.value = await listTestProjectOptionsApi();
  }

  async function loadUsers() {
    if (users.value.length > 0) return;
    await searchUsers('');
  }

  async function searchUsers(
    keyword = '',
    keepIds: Array<null | number | undefined> = [],
  ) {
    const requestGeneration = userOptionsRequestGuard.next();
    const canUseBusinessOptions =
      hasAccessByCodes(['approval:create']) ||
      hasAccessByCodes(['material-flow:transfer']) ||
      hasAccessByCodes(['project:view']) ||
      hasAccessByCodes(['project:create']) ||
      hasAccessByCodes(['project:edit']) ||
      hasAccessByCodes(['material:create']) ||
      hasAccessByCodes(['material:edit']);
    userOptionsLoading.value = true;
    try {
      let incoming: (UserDto | UserOptionDto)[] = [];
      if (canUseBusinessOptions) {
        const result = await getUserOptionsPageApi(keyword, 1, 50);
        incoming = result.items;
      } else if (hasAccessByCodes(['user:view'])) {
        const result = await getUserListApi(keyword, 1, 50);
        incoming = result.items.filter((user) => user.isActive);
      }
      if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
      users.value = replaceUserOptions(users.value, incoming, [
        ...projectFilter.ownerIds,
        form.ownerId,
        ...keepIds,
      ]);
    } catch {
      // 请求层已提示，保留已回填选项。
    } finally {
      if (userOptionsRequestGuard.isLatest(requestGeneration))
        userOptionsLoading.value = false;
    }
  }

  async function loadBaseOptions() {
    // 只用于料件归属部门选择，按后端可归属范围收窄。
    const departmentTree = await getDepartmentOptionsApi('assignable');
    departments.value = departmentTree;
  }

  async function exportProjects() {
    try {
      const response = await exportTestProjectsApi(
        buildProjectPageQuery(
          projectFilter,
          deleteStatus.value,
          projectQuery.page,
          projectQuery.pageSize,
        ),
      );
      downloadBlob(response.data, `测试项目_${localTodayText()}.xlsx`);
    } catch {
      // 错误已由 request.ts 拦截器统一弹出
    }
  }

  return {
    loadData,
    searchProjects,
    resetProjectFilter,
    loadOptions,
    loadUsers,
    searchUsers,
    loadBaseOptions,
    exportProjects,
  };
}
