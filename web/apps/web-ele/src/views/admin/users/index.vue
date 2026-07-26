<script lang="ts" setup>
import type { DepartmentOptionNode } from '#/api/base-data';
import type { RoleDto } from '#/api/role';
import type { UserDto, UserImportRow, UserOptionDto } from '#/api/user';

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
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { getDepartmentOptionsApi } from '#/api/base-data';
import { getRoleListApi } from '#/api/role';
import {
  createUserApi,
  deleteUserApi,
  downloadUserImportTemplateApi,
  getUserListApi,
  importUsersApi,
  resetUserPasswordApi,
  toggleUserStatusApi,
  updateUserApi,
  validateUserImportApi,
} from '#/api/user';
import { flattenActiveDepartments } from '#/utils/department-options';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import {
  mergeSelectedUserOption,
  mergeUserOptions,
} from '#/utils/user-options';
import { buildUserActionAccess } from '#/views/permissions/action-access';

import {
  buildUserPayload,
  resolveDefaultEmployeeRoleId,
  resolveDepartmentSupervisor,
  userToForm,
} from './user-form';

defineOptions({ name: 'AdminUsers' });

const listRequestGuard = createLatestRequestGuard();

const { hasAccessByCodes } = useAccess();
const userActionAccess = computed(() =>
  buildUserActionAccess(hasAccessByCodes),
);
const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const importDialogVisible = ref(false);
const importing = ref(false);
const editingId = ref<null | number>(null);
const users = ref<UserDto[]>([]);
const supervisorOptions = ref<UserOptionDto[]>([]);
const supervisorOptionsLoading = ref(false);
const supervisorOptionsRequestGuard = createLatestRequestGuard();
const importValidationGuard = createLatestRequestGuard();
const roles = ref<RoleDto[]>([]);
const departments = ref<DepartmentOptionNode[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));
const importFileInput = ref<HTMLInputElement | null>(null);
const selectedImportFile = ref<File | null>(null);
const importRows = ref<UserImportRow[]>([]);

const query = reactive({
  departmentId: undefined as number | undefined,
  keyword: '',
  page: 1,
  pageSize: 20,
  roleId: undefined as number | undefined,
});

const form = reactive({
  employeeNo: '',
  name: '',
  phone: '',
  email: '',
  departmentId: undefined as number | undefined,
  roleId: undefined as number | undefined,
  roleName: '',
  supervisorId: undefined as number | undefined,
});

const departmentOptions = computed(() =>
  flattenActiveDepartments(departments.value),
);

async function loadRoles() {
  if (!userActionAccess.value.canViewRoles) return;
  const result = await getRoleListApi(undefined, 1, 100);
  roles.value = result.items;
  if (
    dialogVisible.value &&
    editingId.value === null &&
    form.roleId === undefined
  ) {
    form.roleId = resolveDefaultEmployeeRoleId(roles.value);
  }
}

async function loadDepartments() {
  if (!userActionAccess.value.canCreate && !userActionAccess.value.canEdit)
    return;
  departments.value = await getDepartmentOptionsApi();
}

async function loadData() {
  const requestGeneration = listRequestGuard.next();
  loading.value = true;
  try {
    const result = await getUserListApi(
      query.keyword,
      query.page,
      query.pageSize,
      query.departmentId,
      query.roleId,
    );
    if (!listRequestGuard.isLatest(requestGeneration)) return;
    users.value = result.items;
    total.value = result.total;
  } finally {
    if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    employeeNo: '',
    name: '',
    phone: '',
    email: '',
    departmentId: undefined,
    roleId: resolveDefaultEmployeeRoleId(roles.value),
    roleName: userActionAccess.value.canAssignRole ? '' : '普通员工',
    supervisorId: undefined,
  });
  dialogVisible.value = true;
  runHandled(searchSupervisors(''));
}

function openEdit(row: UserDto) {
  editingId.value = row.id;
  Object.assign(form, userToForm(row));
  supervisorOptions.value = mergeSelectedUserOption(supervisorOptions.value, {
    id: row.supervisorId,
    name: row.supervisorName,
  });
  dialogVisible.value = true;
  runHandled(searchSupervisors(''));
}

async function searchSupervisors(keyword = '') {
  const generation = supervisorOptionsRequestGuard.next();
  supervisorOptionsLoading.value = true;
  try {
    const result = await getUserListApi(keyword, 1, 50);
    if (!supervisorOptionsRequestGuard.isLatest(generation)) return;
    supervisorOptions.value = mergeUserOptions(
      supervisorOptions.value,
      result.items.filter(
        (user) => user.isActive && user.id !== editingId.value,
      ),
    );
  } catch {
    // 请求层已提示，保留现有选项。
  } finally {
    if (supervisorOptionsRequestGuard.isLatest(generation))
      supervisorOptionsLoading.value = false;
  }
}

function onDepartmentChange(departmentId?: number) {
  const selection = resolveDepartmentSupervisor(
    departmentOptions.value,
    departmentId,
    editingId.value,
  );
  form.supervisorId = selection.supervisorId;
  supervisorOptions.value = mergeSelectedUserOption(supervisorOptions.value, {
    id: selection.supervisorId,
    name: selection.supervisorName,
  });
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('请填写用户名称');
    return;
  }
  if (!editingId.value && !form.employeeNo.trim()) {
    ElMessage.warning('新增用户需要填写工号');
    return;
  }
  if (userActionAccess.value.canAssignRole && !form.roleId) {
    ElMessage.warning('请选择角色');
    return;
  }

  saving.value = true;
  try {
    const payload = buildUserPayload(form);

    await (editingId.value
      ? updateUserApi(editingId.value, payload)
      : createUserApi({
          employeeNo: form.employeeNo,
          ...payload,
        }));
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    query.page = 1;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function resetPassword(row: UserDto) {
  try {
    await ElMessageBox.confirm(
      `确认重置用户「${row.name}」的密码？重置后默认密码为 123456`,
      '重置密码',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  await resetUserPasswordApi(row.id);
  ElMessage.success('密码已重置');
}

async function toggleStatus(row: UserDto) {
  const action = row.isActive ? '禁用' : '启用';
  try {
    await ElMessageBox.confirm(`确认${action}用户「${row.name}」？`, '确认', {
      type: 'warning',
    });
  } catch {
    return;
  }
  await toggleUserStatusApi(row.id, !row.isActive);
  ElMessage.success(`${action}成功`);
  await loadData();
}

async function remove(row: UserDto) {
  try {
    await ElMessageBox.confirm(`确认删除用户「${row.name}」？`, '删除确认', {
      type: 'warning',
    });
  } catch {
    return;
  }
  await deleteUserApi(row.id);
  ElMessage.success('删除成功');
  await loadData();
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

async function downloadImportTemplate() {
  const response = await downloadUserImportTemplateApi();
  downloadBlob(response.data, 'user-import-template.xlsx');
}

function openImport() {
  selectedImportFile.value = null;
  importRows.value = [];
  if (importFileInput.value) {
    importFileInput.value.value = '';
  }
  importDialogVisible.value = true;
}

function chooseImportFile() {
  if (importFileInput.value) {
    importFileInput.value.value = '';
    importFileInput.value.click();
  }
}

async function onImportFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  selectedImportFile.value = input.files?.[0] ?? null;
  importRows.value = [];
  if (!selectedImportFile.value) {
    return;
  }

  const requestGeneration = importValidationGuard.next();
  importing.value = true;
  try {
    const result = await validateUserImportApi(selectedImportFile.value);
    if (!importValidationGuard.isLatest(requestGeneration)) return;
    importRows.value = result.rows;
    if (result.failedCount > 0) {
      ElMessage.warning(
        `预览发现 ${result.failedCount} 条错误，请修正后重新选择文件`,
      );
    } else {
      ElMessage.success(`预览通过 ${result.successCount} 条`);
    }
  } finally {
    if (importValidationGuard.isLatest(requestGeneration)) {
      importing.value = false;
    }
  }
}

async function confirmImportUsers() {
  if (!selectedImportFile.value) {
    ElMessage.warning('请先选择 Excel 文件');
    return;
  }

  importing.value = true;
  try {
    const result = await importUsersApi(selectedImportFile.value);
    importRows.value = result.rows;
    if (result.failedCount > 0) {
      ElMessage.warning(`导入失败 ${result.failedCount} 条，请修正后重新导入`);
      return;
    }
    ElMessage.success(`导入成功 ${result.successCount} 条`);
    importDialogVisible.value = false;
    query.page = 1;
    await loadData();
  } finally {
    importing.value = false;
  }
}

function search() {
  query.page = 1;
  runHandled(loadData());
}

function reset() {
  query.departmentId = undefined;
  query.keyword = '';
  query.page = 1;
  query.roleId = undefined;
  runHandled(loadData());
}

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await Promise.all([loadRoles(), loadDepartments()]);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="user-page">
      <div class="user-header">
        <div>
          <h2 class="user-title">用户管理</h2>
        </div>
        <div class="user-header-actions">
          <ElButton v-if="userActionAccess.canImport" @click="openImport">
            批量导入
          </ElButton>
          <ElButton
            v-if="userActionAccess.canCreate"
            type="primary"
            @click="openCreate"
          >
            新增用户
          </ElButton>
        </div>
      </div>

      <div class="filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="搜索">
            <ElInput
              v-model="query.keyword"
              clearable
              placeholder="工号或姓名"
              style="width: 240px"
              @keyup.enter="search"
            />
          </ElFormItem>
          <ElFormItem label="部门">
            <ElSelect
              v-model="query.departmentId"
              clearable
              filterable
              placeholder="全部部门"
              style="width: 200px"
            >
              <ElOption
                v-for="department in departmentOptions"
                :key="department.id"
                :label="department.label"
                :value="department.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="角色">
            <ElSelect
              v-model="query.roleId"
              clearable
              filterable
              placeholder="全部角色"
              style="width: 180px"
            >
              <ElOption
                v-for="role in roles"
                :key="role.id"
                :label="role.name"
                :value="role.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="reset">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <div class="user-table-panel">
        <ElTable :data="users" border height="100%" v-loading="loading">
          <ElTableColumn label="工号" min-width="120" prop="employeeNo" />
          <ElTableColumn label="姓名" min-width="140" prop="name" />
          <ElTableColumn
            class-name="hide-on-mobile"
            label="部门"
            min-width="150"
            prop="departmentName"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              {{ row.departmentName || '--' }}
            </template>
          </ElTableColumn>
          <ElTableColumn
            class-name="hide-on-mobile"
            label="邮箱"
            min-width="180"
            prop="email"
          />
          <ElTableColumn
            class-name="hide-on-mobile"
            label="角色"
            min-width="180"
          >
            <template #default="{ row }">
              <template v-if="row.roleNames && row.roleNames.length > 0">
                <ElTag
                  v-for="role in row.roleNames"
                  :key="role"
                  size="small"
                  style="margin-right: 4px"
                >
                  {{ role }}
                </ElTag>
              </template>
              <span v-else class="user-empty-text">--</span>
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" label="状态" width="100">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'danger'" size="small">
                {{ row.isActive ? '启用' : '禁用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" fixed="right" label="操作" width="300">
            <template #default="{ row }">
              <ElButton
                v-if="userActionAccess.canEdit"
                link
                size="small"
                type="primary"
                @click="openEdit(row)"
              >
                编辑
              </ElButton>
              <ElButton
                v-if="userActionAccess.canResetPassword"
                link
                size="small"
                type="primary"
                @click="resetPassword(row)"
              >
                重置密码
              </ElButton>
              <ElButton
                v-if="userActionAccess.canToggleStatus"
                :disabled="loading"
                :type="row.isActive ? 'danger' : 'primary'"
                link
                size="small"
                @click="toggleStatus(row)"
              >
                {{ row.isActive ? '禁用' : '启用' }}
              </ElButton>
              <ElButton
                v-if="userActionAccess.canDelete && row.canManage"
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
            <span>共 {{ total }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect
              v-model="query.pageSize"
              aria-label="用户列表每页条数"
              style="width: 92px"
              @change="search"
            >
              <ElOption
                v-for="size in pageSizeOptions"
                :key="size"
                :label="`${size}`"
                :value="size"
              />
            </ElSelect>
          </div>
          <ElPagination
            v-model:current-page="query.page"
            :page-size="query.pageSize"
            :total="total"
            background
            layout="prev, pager, next"
            @current-change="loadData"
          />
        </div>
      </div>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑用户' : '新增用户'"
        width="540px"
      >
        <ElForm class="user-edit-form" label-width="100px">
          <ElFormItem label="工号" required>
            <ElInput
              v-model="form.employeeNo"
              :disabled="!!editingId"
              placeholder="新增用户时必填"
            />
          </ElFormItem>
          <ElFormItem label="姓名" required>
            <ElInput v-model="form.name" placeholder="请输入姓名" />
          </ElFormItem>
          <ElFormItem label="邮箱">
            <ElInput
              v-model="form.email"
              clearable
              placeholder="请输入邮箱"
              type="email"
            />
          </ElFormItem>
          <ElFormItem label="手机号">
            <ElInput
              v-model="form.phone"
              clearable
              placeholder="请输入手机号"
            />
          </ElFormItem>
          <ElFormItem label="部门">
            <ElSelect
              v-model="form.departmentId"
              clearable
              filterable
              placeholder="选择部门"
              style="width: 100%"
              @change="onDepartmentChange"
            >
              <ElOption
                v-for="department in departmentOptions"
                :key="department.id"
                :disabled="!department.isActive"
                :label="department.label"
                :value="department.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="直属主管">
            <ElSelect
              v-model="form.supervisorId"
              :loading="supervisorOptionsLoading"
              :remote-method="searchSupervisors"
              clearable
              filterable
              placeholder="选择直属主管"
              remote
              style="width: 100%"
            >
              <ElOption
                v-for="supervisor in supervisorOptions"
                :key="supervisor.id"
                :label="
                  supervisor.employeeNo
                    ? `${supervisor.name}（${supervisor.employeeNo}）`
                    : supervisor.name
                "
                :value="supervisor.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="角色" required>
            <ElSelect
              v-if="userActionAccess.canAssignRole"
              v-model="form.roleId"
              filterable
              placeholder="选择角色"
              style="width: 100%"
            >
              <ElOption
                v-for="role in roles"
                :key="role.id"
                :label="role.name"
                :value="role.id"
              />
            </ElSelect>
            <ElInput v-else :model-value="form.roleName || '未分配'" disabled />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">
            保存
          </ElButton>
        </template>
      </ElDialog>

      <ElDialog
        v-model="importDialogVisible"
        title="批量导入用户"
        width="920px"
      >
        <div class="user-import-toolbar">
          <ElButton @click="downloadImportTemplate">下载模板</ElButton>
          <input
            ref="importFileInput"
            accept=".xlsx"
            class="user-import-file-input"
            type="file"
            @change="onImportFileChange"
          />
          <ElButton @click="chooseImportFile">选择文件</ElButton>
          <span class="user-import-file-name">
            {{ selectedImportFile?.name || '未选择文件' }}
          </span>
          <ElButton
            :disabled="
              importing ||
              importRows.length === 0 ||
              importRows.some((row) => !row.isValid)
            "
            :loading="importing"
            type="primary"
            @click="confirmImportUsers"
          >
            确认导入
          </ElButton>
        </div>
        <ElTable :data="importRows" border max-height="360">
          <ElTableColumn label="行号" prop="row" width="80" />
          <ElTableColumn label="工号" min-width="120" prop="employeeNo" />
          <ElTableColumn label="姓名" min-width="120" prop="name" />
          <ElTableColumn label="邮箱" min-width="180" prop="email" />
          <ElTableColumn
            label="部门名称"
            min-width="140"
            prop="departmentName"
          />
          <ElTableColumn label="角色名称" min-width="140" prop="roleName" />
          <ElTableColumn label="状态" width="90">
            <template #default="{ row }">
              <ElTag :type="row.isValid ? 'success' : 'danger'">
                {{ row.isValid ? '有效' : '无效' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="错误" min-width="220" prop="error" />
        </ElTable>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
/* ========== 设计系统规范 ========== */
.user-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}

/* ========== 页面头部 ========== */
.user-header {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border-strong);
  border-radius: 12px;
  box-shadow: var(--asset-page-shadow);
}

.user-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
  color: var(--asset-page-text);
  letter-spacing: 0;
}

.user-header-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

/* ========== 筛选面板 ========== */
.user-filter-panel {
  flex-shrink: 0;
  padding: 16px 20px;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  box-shadow: var(--asset-page-shadow);
}

.user-search-form :deep(.el-form-item) {
  margin-right: 12px;
  margin-bottom: 0;
}

.user-search-form :deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
}

/* ========== 表格面板 ========== */
.user-table-panel {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  box-shadow: var(--asset-page-shadow);
}

.user-empty-text {
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.user-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
  font-size: 14px;
  line-height: 20px;
}

.user-table-panel :deep(.el-table th.el-table__cell) {
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
  background: var(--asset-page-surface-soft);
}

.user-table-panel :deep(.el-table--border) {
  border: none;
}

.user-table-panel :deep(.el-table td.el-table__cell),
.user-table-panel :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.user-table-panel :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

.user-table-panel :deep(.el-button + .el-button) {
  margin-left: 4px;
}

/* ========== 对话框优化 ========== */
:deep(.el-dialog) {
  border-radius: 12px;
}

:deep(.el-dialog__header) {
  padding: 20px 24px;
  border-bottom: 1px solid var(--asset-page-border);
}

:deep(.el-dialog__body) {
  padding: 24px;
}

:deep(.el-dialog__footer) {
  padding: 16px 24px;
  border-top: 1px solid var(--asset-page-border);
}

:deep(.el-form-item) {
  margin-bottom: 20px;
}

:deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
}

.user-edit-form :deep(.el-form-item__label) {
  align-items: center;
  line-height: var(--el-component-size);
}

.user-import-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
  margin-bottom: 14px;
}

.user-import-file-input {
  display: none;
}

.user-import-file-name {
  min-width: 180px;
  max-width: 320px;
  overflow: hidden;
  font-size: 14px;
  line-height: 32px;
  color: var(--asset-page-text-secondary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.el-input__inner) {
  font-size: 14px;
  line-height: 20px;
}
</style>
