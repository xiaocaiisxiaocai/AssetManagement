import type { MaterialQuery } from '#/api/material';

import { listMaterialsApi } from '#/api/material';
import { nonempty } from '#/api/repeat-params';
import { runHandled } from '#/utils/handled-promise';
import { normalizedPage } from '#/utils/pagination';

export function bindProjectMaterialQueryActions(
  page: Record<string, any>,
  deps: {
    loadData: () => Promise<void>;
  },
) {
  const { loadData } = deps;
  const {
    currentProject,
    projectQuery,
    materialLoading,
    materials,
    materialTotal,
    followupDrawerVisible,
    materialRequestGuard,
    materialQuery,
  } = page;

  function buildMaterialQuery(projectId: number): MaterialQuery {
    return {
      deleteStatus: materialQuery.deleteStatus,
      materialNo: materialQuery.materialNo || undefined,
      name: materialQuery.name || undefined,
      page: materialQuery.page,
      pageSize: materialQuery.pageSize,
      projectId,
      statuses: nonempty(materialQuery.statuses),
    };
  }

  async function loadProjectMaterials(projectId = currentProject.value?.id) {
    if (!projectId) return;
    const requestGeneration = materialRequestGuard.next();
    materialLoading.value = true;
    try {
      const result = await listMaterialsApi(buildMaterialQuery(projectId));
      if (
        materialRequestGuard.isLatest(requestGeneration) &&
        followupDrawerVisible.value &&
        currentProject.value?.id === projectId
      ) {
        const validPage = normalizedPage(
          materialQuery.page,
          result.total,
          materialQuery.pageSize,
        );
        if (materialQuery.page !== validPage) {
          materialQuery.page = validPage;
          await loadProjectMaterials(projectId);
          return;
        }
        materials.value = result.items;
        materialTotal.value = result.total;
      }
    } finally {
      if (materialRequestGuard.isLatest(requestGeneration)) {
        materialLoading.value = false;
      }
    }
  }

  function searchMaterials() {
    materialQuery.page = 1;
    runHandled(loadProjectMaterials());
  }

  function resetMaterialQuery() {
    Object.assign(materialQuery, {
      deleteStatus: 'all',
      materialNo: '',
      name: '',
      page: 1,
      statuses: [],
    });
    runHandled(loadProjectMaterials());
  }

  function onProjectPageSizeChange() {
    projectQuery.page = 1;
    runHandled(loadData());
  }

  function onProjectDeleteStatusChange() {
    projectQuery.page = 1;
    runHandled(loadData());
  }

  function onMaterialPageSizeChange() {
    materialQuery.page = 1;
    runHandled(loadProjectMaterials());
  }

  return {
    buildMaterialQuery,
    loadProjectMaterials,
    searchMaterials,
    resetMaterialQuery,
    onProjectPageSizeChange,
    onProjectDeleteStatusChange,
    onMaterialPageSizeChange,
  };
}
