import type { RoleFormState, RoleListQuery } from './role-form';

import type { MenuDto, PermissionDto, RoleDto } from '#/api/role';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { buildRoleActionAccess } from '#/views/permissions/action-access';

import {
  collectRequiredPermissionIds,
  filterPageMenuTree,
} from './menu-tree-selection';
import { buildPermissionGroups } from './permission-groups';
import { bindRoleAccessActions } from './role-access-actions';
import { bindRoleRecordActions } from './role-record-actions';

export function useRoleAdmin() {
  const listRequestGuard = createLatestRequestGuard();

  const { hasAccessByCodes } = useAccess();
  const roleActionAccess = computed(() =>
    buildRoleActionAccess(hasAccessByCodes),
  );
  const loading = ref(false);
  const saving = ref(false);
  const dialogVisible = ref(false);
  const accessDialogVisible = ref(false);
  const editingId = ref<null | number>(null);
  const roles = ref<RoleDto[]>([]);
  const permissions = ref<PermissionDto[]>([]);
  const menus = ref<MenuDto[]>([]);
  const total = ref(0);
  const activePermissionGroupKey = ref('');
  const permissionKeyword = ref('');
  const showSelectedOnly = ref(false);
  const pageSizeOptions = ref(createPageSizeOptions(20));

  const query = reactive<RoleListQuery>({
    keyword: '',
    page: 1,
    pageSize: 20,
  });

  const form = reactive<RoleFormState>({
    code: '',
    name: '',
    isActive: true,
  });
  const queryModel = reactiveObjectModel(query);
  const formModel = reactiveObjectModel(form);

  const accessForm = reactive({
    roleId: 0 as number,
    roleName: '',
    selectedPermissions: [] as number[],
    selectedMenus: [] as number[],
  });
  const selectedPermissionsModel = computed({
    get: () => accessForm.selectedPermissions,
    set: (value) => {
      accessForm.selectedPermissions = value;
    },
  });

  const canConfigureAccess = computed(
    () =>
      roleActionAccess.value.canAssignPermission &&
      roleActionAccess.value.canAssignMenu,
  );
  const pageMenuTree = computed(() => filterPageMenuTree(menus.value));
  const selectedPermissionSet = computed(
    () => new Set(accessForm.selectedPermissions),
  );
  const requiredPermissionIdSet = computed(
    () =>
      new Set(
        collectRequiredPermissionIds(
          pageMenuTree.value,
          permissions.value,
          accessForm.selectedMenus,
        ),
      ),
  );

  const permissionGroups = computed(() =>
    buildPermissionGroups({
      menus: menus.value,
      permissions: permissions.value,
      selectedPermissionIds: accessForm.selectedPermissions,
    }),
  );

  const activePermissionGroup = computed(() => {
    if (!activePermissionGroupKey.value && permissionGroups.value.length > 0) {
      return permissionGroups.value[0];
    }
    return (
      permissionGroups.value.find(
        (item) => item.key === activePermissionGroupKey.value,
      ) ?? permissionGroups.value[0]
    );
  });

  const filteredPermissions = computed(() => {
    const group = activePermissionGroup.value;
    if (!group) return [];

    const keyword = permissionKeyword.value.trim().toLowerCase();
    return group.permissions.filter((perm) => {
      const matchedKeyword =
        !keyword ||
        perm.name.toLowerCase().includes(keyword) ||
        perm.code.toLowerCase().includes(keyword);
      const matchedSelected =
        !showSelectedOnly.value || selectedPermissionSet.value.has(perm.id);
      return matchedKeyword && matchedSelected;
    });
  });

  const activePermissionGroupKeyValue = computed(
    () => activePermissionGroup.value?.key ?? '',
  );
  const activePermissionGroupLabel = computed(
    () => activePermissionGroup.value?.label ?? '权限',
  );

  const bindRoleRecordActionsResult = bindRoleRecordActions({
    listRequestGuard,
    loading,
    saving,
    dialogVisible,
    accessDialogVisible,
    editingId,
    roles,
    permissions,
    menus,
    total,
    activePermissionGroupKey,
    permissionKeyword,
    showSelectedOnly,
    query,
    form,
    accessForm,
    canConfigureAccess,
    pageMenuTree,
    permissionGroups,
  });
  const {
    loadData,
    loadPermissionsAndMenus,
    openCreate,
    openEdit,
    save,
    openAccessDialog,
    syncRequiredPermissions,
    remove,
    search,
    reset,
  } = bindRoleRecordActionsResult;

  const bindRoleAccessActionsResult = bindRoleAccessActions({
    saving,
    accessDialogVisible,
    activePermissionGroupKey,
    accessForm,
    pageMenuTree,
    requiredPermissionIdSet,
    activePermissionGroup,
    loadData,
    syncRequiredPermissions,
  });
  const {
    selectPermissionGroup,
    selectCurrentModulePermissions,
    clearCurrentModulePermissions,
    syncMenuSelection,
    saveAccess,
    leafMenuIds,
    flattenMenus,
  } = bindRoleAccessActionsResult;

  onMounted(async () => {
    query.pageSize = await getDefaultPageSize();
    pageSizeOptions.value = createPageSizeOptions(query.pageSize);
    await loadPermissionsAndMenus();
    await loadData();
  });

  return {
    listRequestGuard,
    roleActionAccess,
    loading,
    saving,
    dialogVisible,
    accessDialogVisible,
    editingId,
    roles,
    permissions,
    menus,
    total,
    activePermissionGroupKey,
    permissionKeyword,
    showSelectedOnly,
    pageSizeOptions,
    query,
    form,
    queryModel,
    formModel,
    accessForm,
    selectedPermissionsModel,
    canConfigureAccess,
    pageMenuTree,
    selectedPermissionSet,
    requiredPermissionIdSet,
    permissionGroups,
    activePermissionGroup,
    filteredPermissions,
    activePermissionGroupKeyValue,
    activePermissionGroupLabel,
    selectPermissionGroup,
    selectCurrentModulePermissions,
    clearCurrentModulePermissions,
    loadData,
    loadPermissionsAndMenus,
    openCreate,
    openEdit,
    save,
    openAccessDialog,
    syncMenuSelection,
    syncRequiredPermissions,
    saveAccess,
    leafMenuIds,
    flattenMenus,
    remove,
    search,
    reset,
  };
}
