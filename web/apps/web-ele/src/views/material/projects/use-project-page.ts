import type { OptionKind } from './project-workspace-types';

import { computed, onMounted, watch } from 'vue';

import { useAccess } from '@vben/access';
import { useUserStore } from '@vben/stores';

import { flattenActiveDepartments } from '#/utils/department-options';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import {
  buildMaterialActionAccess,
  buildProjectActionAccess,
} from '#/views/permissions/action-access';

import { bindProjectCatalogActions } from './project-catalog-actions';
import { createProjectPageState } from './project-page-state';
import { bindProjectWorkspaceActions } from './project-workspace-actions';

export function useProjectPage() {
  const { hasAccessByCodes } = useAccess();
  const userStore = useUserStore();
  const state = createProjectPageState();
  const projectActionAccess = computed(() =>
    buildProjectActionAccess(hasAccessByCodes),
  );
  const materialActionAccess = computed(() =>
    buildMaterialActionAccess(hasAccessByCodes),
  );
  const isCurrentProjectReadOnly = computed(
    () => state.currentProject.value?.isDeleted === true,
  );
  const currentMaterialActionAccess = computed(() => {
    if (!isCurrentProjectReadOnly.value) return materialActionAccess.value;
    return Object.fromEntries(
      Object.keys(materialActionAccess.value).map((key) => [key, false]),
    ) as typeof materialActionAccess.value;
  });
  const isCurrentProjectOwner = computed(
    () =>
      !!state.currentProject.value?.ownerId &&
      String(state.currentProject.value.ownerId) ===
        String(userStore.userInfo?.userId ?? ''),
  );
  const currentUserId = computed(() => Number(userStore.userInfo?.userId ?? 0));
  const isCurrentUserSupervisor = computed(() =>
    userStore.userRoles.includes('supervisor'),
  );
  const canWriteCurrentProjectMaterial = computed(
    () =>
      !isCurrentProjectReadOnly.value &&
      (materialActionAccess.value.canCreate || isCurrentProjectOwner.value),
  );
  const canEditCurrentProjectMaterial = computed(
    () =>
      !isCurrentProjectReadOnly.value &&
      (materialActionAccess.value.canEdit || isCurrentProjectOwner.value),
  );
  const canUpdateCurrentProjectProgress = computed(
    () =>
      !isCurrentProjectReadOnly.value &&
      !state.currentProject.value?.closedDate &&
      (isCurrentProjectOwner.value || hasAccessByCodes(['project:manage'])),
  );
  const projectTypeOptions = computed(() => activeOptions('project_type'));
  const progressOptions = computed(() => activeOptions('project_progress'));
  const displayedOptions = computed(() =>
    state.options.value.filter(
      (item) => item.kind === state.activeOptionKind.value,
    ),
  );
  const activeDepartmentOptions = computed(() =>
    flattenActiveDepartments(state.departments.value),
  );
  const currentProjectList = computed(() =>
    state.currentProject.value
      ? [state.currentProject.value]
      : state.projects.value,
  );
  const projectOwnerOptions = computed(() => {
    const ownerMap = new Map<number, { id: number; name: string }>();
    for (const user of state.users.value) {
      ownerMap.set(user.id, { id: user.id, name: user.name });
    }
    for (const project of state.projects.value) {
      if (project.ownerId && project.ownerName) {
        ownerMap.set(project.ownerId, {
          id: project.ownerId,
          name: project.ownerName,
        });
      }
    }
    return [...ownerMap.values()];
  });

  function activeOptions(kind: OptionKind) {
    return state.options.value.filter(
      (item) => item.kind === kind && item.isActive,
    );
  }

  function normalizeText(value?: null | string) {
    const text = value?.trim();
    return text || null;
  }

  function generateOptionCode(kind: OptionKind) {
    const prefix = kind === 'project_type' ? 'type' : 'progress';
    return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`;
  }

  const catalog = bindProjectCatalogActions({
    ...state,
    generateOptionCode,
    hasAccessByCodes,
    normalizeText,
  });
  const workspace = bindProjectWorkspaceActions({
    ...state,
    ...catalog,
    currentMaterialActionAccess,
    isCurrentProjectReadOnly,
    materialActionAccess,
  });

  onMounted(async () => {
    const defaultPageSize = await getDefaultPageSize();
    state.projectQuery.pageSize = defaultPageSize;
    state.materialQuery.pageSize = defaultPageSize;
    state.pendingFlowQuery.pageSize = defaultPageSize;
    state.handledFlowQuery.pageSize = defaultPageSize;
    state.myFlowQuery.pageSize = defaultPageSize;
    state.pageSizeOptions.value = createPageSizeOptions(defaultPageSize);
    state.materialPageSizeOptions.value =
      createPageSizeOptions(defaultPageSize);
    state.flowPageSizeOptions.value = createPageSizeOptions(defaultPageSize);
    await Promise.all([catalog.loadOptions(), catalog.loadData()]);
  });

  watch(state.followupDrawerVisible, (opened) => {
    if (opened) return;
    state.followupRequestGuard.invalidate();
    state.materialRequestGuard.invalidate();
    state.pendingFlowRequestGuard.invalidate();
    state.handledFlowRequestGuard.invalidate();
    state.myFlowRequestGuard.invalidate();
    state.followupLoading.value = false;
    state.materialLoading.value = false;
    state.pendingFlowLoading.value = false;
    state.handledFlowLoading.value = false;
    state.myFlowLoading.value = false;
  });

  watch(state.materialDetailVisible, (opened) => {
    if (opened) return;
    state.materialDetailRequestGuard.invalidate();
    state.materialDetailLoading.value = false;
    state.materialDetail.value = null;
  });

  return {
    ...state,
    ...catalog,
    ...workspace,
    activeDepartmentOptions,
    activeOptions,
    canEditCurrentProjectMaterial,
    canUpdateCurrentProjectProgress,
    canWriteCurrentProjectMaterial,
    currentMaterialActionAccess,
    currentProjectList,
    currentUserId,
    displayedOptions,
    generateOptionCode,
    isCurrentProjectOwner,
    isCurrentProjectReadOnly,
    isCurrentUserSupervisor,
    materialActionAccess,
    normalizeText,
    progressOptions,
    projectActionAccess,
    projectOwnerOptions,
    projectTypeOptions,
    userStore,
  };
}
