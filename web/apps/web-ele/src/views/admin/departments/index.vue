<script lang="ts" setup>
import type {
  DepartmentNode,
  DepartmentPayload,
  OrganizationLevel,
} from '#/api/base-data';
import type { UserOptionDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElSelect,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTreeSelect,
} from 'element-plus';

import {
  createDepartmentApi,
  deleteDepartmentApi,
  getDepartmentTreeApi,
  getOrganizationLevelsApi,
  updateDepartmentApi,
} from '#/api/base-data';
import { getUserListApi, getUserOptionsPageApi } from '#/api/user';
import { createLatestRequestGuard } from '#/utils/latest-request';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { mergeUserOptions } from '#/utils/user-options';
import { buildDepartmentActionAccess } from '#/views/permissions/action-access';

import {
  getAllowedOrganizationLevelCodes,
  getDefaultOrganizationLevelCode,
} from './organization-hierarchy';

defineOptions({ name: 'AdminDepartments' });

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
const userOptionsRequestGuard = createLatestRequestGuard();
const pageSizeOptions = ref(createPageSizeOptions(20));
const page = ref(1);
const pageSize = ref(20);
type DepartmentForm = {
  managerId?: number;
} & Omit<DepartmentPayload, 'managerId'>;

const form = reactive<DepartmentForm>({
  isActive: true,
  managerId: undefined,
  name: '',
  organizationLevelCode: '',
  parentId: null,
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
  return getAllowedOrganizationLevelCodes(row.organizationLevelCode).length > 0;
}

function onParentChange() {
  const parentLevelCode = findDepartment(form.parentId)?.organizationLevelCode;
  const allowedCodes = getAllowedOrganizationLevelCodes(parentLevelCode);
  if (!allowedCodes.includes(form.organizationLevelCode ?? '')) {
    form.organizationLevelCode =
      getDefaultOrganizationLevelCode(parentLevelCode);
  }
}

async function loadUsers() {
  await searchUsers('');
}

async function searchUsers(keyword = '') {
  const requestGeneration = userOptionsRequestGuard.next();
  userOptionsLoading.value = true;
  try {
    const canLoadUserOptions =
      hasAccessByCodes(['approval:create']) ||
      hasAccessByCodes(['material-flow:transfer']) ||
      hasAccessByCodes(['department:create']) ||
      hasAccessByCodes(['department:edit']);
    let incoming: UserOptionDto[];
    if (canLoadUserOptions) {
      const response = await getUserOptionsPageApi(keyword, 1, 50);
      incoming = response.items;
    } else {
      const response = await getUserListApi(keyword, 1, 50);
      incoming = response.items.filter((user) => user.isActive);
    }
    if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
    userOptions.value = mergeUserOptions(userOptions.value, incoming);
  } catch {
    // 请求层已提示，保留已回填选项。
  } finally {
    if (userOptionsRequestGuard.isLatest(requestGeneration))
      userOptionsLoading.value = false;
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
    await ElMessageBox.confirm(`确认删除组织架构「${row.name}」？`, '删除确认', {
      type: 'warning',
    });
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
  await Promise.all([loadUsers(), loadOrganizationLevels(), loadData()]);
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">组织架构管理</h2>
        </div>
        <ElButton
          v-if="departmentActionAccess.canCreate"
          type="primary"
          @click="openCreate()"
        >
          新增组织架构
        </ElButton>
      </div>

      <div class="table-panel">
        <ElTable
          :data="pagedDepartments"
          border
          default-expand-all
          height="100%"
          row-key="id"
          v-loading="loading"
        >
          <ElTableColumn align="center" label="ID" prop="id" width="90" />
          <ElTableColumn label="组织架构名称" min-width="200" prop="name" />
          <ElTableColumn label="组织层级" min-width="120">
            <template #default="{ row }">
              <ElTag size="small" type="info">
                {{ row.organizationLevelName || '未配置' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            class-name="hide-on-mobile"
            label="负责人"
            min-width="140"
            prop="managerName"
          />
          <ElTableColumn align="center" label="状态" min-width="100">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
                {{ row.isActive ? '启用' : '停用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" fixed="right" label="操作" width="240">
            <template #default="{ row }">
              <ElButton
                v-if="departmentActionAccess.canCreate && canCreateChild(row)"
                link
                size="small"
                type="primary"
                @click="openCreate(row)"
              >
                新增下级
              </ElButton>
              <ElButton
                v-if="departmentActionAccess.canEdit"
                link
                size="small"
                type="primary"
                @click="openEdit(row)"
              >
                编辑
              </ElButton>
              <ElButton
                v-if="departmentActionAccess.canDelete"
                link
                size="small"
                type="danger"
                @click="remove(row)"
              >
                删除
              </ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ departments.length }} 条</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect
              v-model="pageSize"
              style="width: 92px"
              @change="onPageSizeChange"
            >
              <ElOption
                v-for="size in pageSizeOptions"
                :key="size"
                :label="`${size} 条`"
                :value="size"
              />
            </ElSelect>
          </div>
          <ElPagination
            v-model:current-page="page"
            :page-size="pageSize"
            :total="departments.length"
            background
            layout="prev, pager, next, jumper"
          />
        </div>
      </div>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑组织架构' : '新增组织架构'"
        width="500px"
      >
        <ElForm label-width="100px">
          <ElFormItem label="上级组织">
            <ElTreeSelect
              :data="departments"
              :props="{ children: 'children', label: 'name' }"
              check-strictly
              clearable
              node-key="id"
              placeholder="留空为顶级组织"
              style="width: 100%"
              value-key="id"
              v-model="form.parentId"
              @change="onParentChange"
            />
          </ElFormItem>
          <ElFormItem label="组织架构名称" required>
            <ElInput v-model="form.name" placeholder="请输入组织架构名称" />
          </ElFormItem>
          <ElFormItem label="组织层级" required>
            <ElSelect
              v-model="form.organizationLevelCode"
              placeholder="请选择组织层级"
              style="width: 100%"
            >
              <ElOption
                v-for="level in selectableOrganizationLevels"
                :key="level.code"
                :label="level.name"
                :value="level.code"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="负责人">
            <ElSelect
              v-model="form.managerId"
              :loading="userOptionsLoading"
              :remote-method="searchUsers"
              clearable
              filterable
              placeholder="可选，选择该组织节点负责人"
              remote
              style="width: 100%"
            >
              <ElOption
                v-for="user in userOptions"
                :key="user.id"
                :label="`${user.name}（${user.employeeNo}）`"
                :value="user.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="启用状态">
            <ElSwitch v-model="form.isActive" />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">
            保存
          </ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>
