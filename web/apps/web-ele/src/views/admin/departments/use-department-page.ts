import type {
  DepartmentNode,
  DepartmentPayload,
  OrganizationLevel,
} from '#/api/base-data';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createDepartmentApi,
  deleteDepartmentApi,
  getDepartmentTreeApi,
  getOrganizationLevelsApi,
  updateDepartmentApi,
} from '#/api/base-data';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { buildDepartmentActionAccess } from '#/views/permissions/action-access';

import { createDepartmentUserSearch } from './department-user-options';
import {
  getAllowedOrganizationLevelCodes,
  getDefaultOrganizationLevelCode,
} from './organization-hierarchy';

export type DepartmentForm = {
  managerId?: number;
} & Omit<DepartmentPayload, 'managerId'>;

export function useDepartmentPage() {
  const { hasAccessByCodes } = useAccess();
  const departmentActionAccess = computed(() =>
    buildDepartmentActionAccess(hasAccessByCodes),
  );
  const loading = ref(false);
  const saving = ref(false);
  const dialogVisible = ref(false);
  const editingId = ref<null | number>(null);
  const departments = ref<DepartmentNode[]>([]);
  const organizationLevels = ref<OrganizationLevel[]>([]);
  const userOptions = ref<UserOptionDto[]>([]);
  const userOptionsLoading = ref(false);
  const pageSizeOptions = ref(createPageSizeOptions(20));
  const page = ref(1);
  const pageSize = ref(20);
  const form = reactive<DepartmentForm>({
    isActive: true,
    managerId: undefined,
    name: '',
    organizationLevelCode: '',
    parentId: null,
  });
  const formModel = reactiveObjectModel(form);
  const { searchUsers } = createDepartmentUserSearch({
    editingId,
    form,
    hasAccessByCodes,
    userOptions,
    userOptionsLoading,
  });

  const pagedDepartments = computed(() => {
    const start = (page.value - 1) * pageSize.value;
    return departments.value.slice(start, start + pageSize.value);
  });

  function findDepartment(
    id: null | number | undefined,
    nodes = departments.value,
  ): DepartmentNode | undefined {
    if (!id) return undefined;
    for (const node of nodes) {
      if (node.id === id) return node;
      const child = findDepartment(id, node.children);
      if (child) return child;
    }
    return undefined;
  }

  const allowedOrganizationLevelCodes = computed(() =>
    getAllowedOrganizationLevelCodes(
      findDepartment(form.parentId)?.organizationLevelCode,
    ),
  );

  const selectableOrganizationLevels = computed(() =>
    organizationLevels.value.filter((level) =>
      allowedOrganizationLevelCodes.value.includes(level.code),
    ),
  );

  function canCreateChild(row: DepartmentNode) {
    return (
      getAllowedOrganizationLevelCodes(row.organizationLevelCode).length > 0
    );
  }

  function onParentChange() {
    const parentLevelCode = findDepartment(
      form.parentId,
    )?.organizationLevelCode;
    const allowedCodes = getAllowedOrganizationLevelCodes(parentLevelCode);
    if (!allowedCodes.includes(form.organizationLevelCode ?? '')) {
      form.organizationLevelCode =
        getDefaultOrganizationLevelCode(parentLevelCode);
    }
  }

  async function loadData() {
    loading.value = true;
    try {
      departments.value = await getDepartmentTreeApi();
      if ((page.value - 1) * pageSize.value >= departments.value.length) {
        page.value = 1;
      }
    } finally {
      loading.value = false;
    }
  }

  async function loadOrganizationLevels() {
    organizationLevels.value = await getOrganizationLevelsApi();
  }

  function openCreate(parent?: DepartmentNode) {
    const allowedCodes = getAllowedOrganizationLevelCodes(
      parent?.organizationLevelCode,
    );
    if (parent && allowedCodes.length === 0) {
      ElMessage.warning('课别不能新增下级组织');
      return;
    }
    editingId.value = null;
    Object.assign(form, {
      isActive: true,
      managerId: undefined,
      name: '',
      organizationLevelCode: getDefaultOrganizationLevelCode(
        parent?.organizationLevelCode,
      ),
      parentId: parent?.id ?? null,
    });
    dialogVisible.value = true;
  }

  function openEdit(row: DepartmentNode) {
    editingId.value = row.id;
    Object.assign(form, {
      isActive: row.isActive,
      managerId: row.managerId ?? undefined,
      name: row.name,
      organizationLevelCode: row.organizationLevelCode ?? 'department',
      parentId: row.parentId ?? null,
    });
    dialogVisible.value = true;
  }

  async function save() {
    if (!form.name.trim()) {
      ElMessage.warning('请填写组织架构名称');
      return;
    }
    if (form.name.trim().length > 100) {
      ElMessage.warning('组织架构名称不能超过 100 个字符');
      return;
    }
    if (!form.organizationLevelCode) {
      ElMessage.warning('请选择组织层级');
      return;
    }
    saving.value = true;
    try {
      const payload: DepartmentPayload = {
        ...form,
        managerId: form.managerId ?? null,
      };
      await (editingId.value
        ? updateDepartmentApi(editingId.value, payload)
        : createDepartmentApi(payload));
      ElMessage.success('保存成功');
      dialogVisible.value = false;
      await loadData();
    } finally {
      saving.value = false;
    }
  }

  async function remove(row: DepartmentNode) {
    try {
      await ElMessageBox.confirm(
        `确认删除组织架构「${row.name}」？`,
        '删除确认',
        {
          type: 'warning',
        },
      );
    } catch {
      return;
    }
    await deleteDepartmentApi(row.id);
    ElMessage.success('删除成功');
    await loadData();
  }

  function onPageSizeChange() {
    page.value = 1;
  }

  onMounted(async () => {
    pageSize.value = await getDefaultPageSize();
    pageSizeOptions.value = createPageSizeOptions(pageSize.value);
    await Promise.all([searchUsers(''), loadOrganizationLevels(), loadData()]);
  });

  return {
    canCreateChild,
    departmentActionAccess,
    departments,
    dialogVisible,
    editingId,
    formModel,
    loading,
    onPageSizeChange,
    onParentChange,
    openCreate,
    openEdit,
    page,
    pageSize,
    pageSizeOptions,
    pagedDepartments,
    remove,
    save,
    saving,
    searchUsers,
    selectableOrganizationLevels,
    userOptions,
    userOptionsLoading,
  };
}
