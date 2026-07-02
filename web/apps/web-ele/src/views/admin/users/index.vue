<script lang="ts" setup>
import type { DepartmentNode } from '#/api/base-data';
import type { UserDto, UserImportRow } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import { getDepartmentTreeApi } from '#/api/base-data';
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
import { getRoleListApi } from '#/api/role';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';
import { buildUserActionAccess } from '#/views/permissions/action-access';

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

defineOptions({ name: 'AdminUsers' });

const { hasAccessByCodes } = useAccess();
const userActionAccess = computed(() => buildUserActionAccess(hasAccessByCodes));
const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const importDialogVisible = ref(false);
const importing = ref(false);
const editingId = ref<null | number>(null);
const users = ref<UserDto[]>([]);
const roles = ref<any[]>([]);
const departments = ref<DepartmentNode[]>([]);
const total = ref(0);
const pageSizeOptions = ref(createPageSizeOptions(20));
const importFileInput = ref<HTMLInputElement | null>(null);
const selectedImportFile = ref<File | null>(null);
const importRows = ref<UserImportRow[]>([]);

const query = reactive({
  keyword: '',
  page: 1,
  pageSize: 20,
});

const form = reactive({
  employeeNo: '',
  name: '',
  email: '',
  departmentId: undefined as number | undefined,
  roleId: undefined as number | undefined,
});

interface DepartmentOption {
  id: number;
  isActive: boolean;
  label: string;
}

const departmentOptions = computed(() => flattenDepartments(departments.value));

async function loadRoles() {
  const result = await getRoleListApi();
  roles.value = result.items;
}

async function loadDepartments() {
  departments.value = await getDepartmentTreeApi();
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getUserListApi(query.keyword, query.page, query.pageSize);
    users.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    employeeNo: '',
    name: '',
    email: '',
    departmentId: undefined,
    roleId: undefined,
  });
  dialogVisible.value = true;
}

function openEdit(row: UserDto) {
  editingId.value = row.id;
  Object.assign(form, {
    employeeNo: row.employeeNo,
    name: row.name,
    email: row.email ?? '',
    departmentId: row.departmentId ?? undefined,
    roleId: row.roleIds?.[0],
  });
  dialogVisible.value = true;
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
  if (!form.roleId) {
    ElMessage.warning('请选择角色');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      name: form.name,
      email: form.email || null,
      departmentId: form.departmentId ?? null,
      roleIds: form.roleId ? [form.roleId] : [],
    };

    if (editingId.value) {
      await updateUserApi(editingId.value, payload);
    } else {
      await createUserApi({
        employeeNo: form.employeeNo,
        ...payload,
      });
    }
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    query.page = 1;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function resetPassword(row: UserDto) {
  await ElMessageBox.confirm(
    `确认重置用户「${row.name}」的密码？重置后默认密码为 123456`,
    '重置密码',
    {
      type: 'warning',
    }
  );
  await resetUserPasswordApi(row.id);
  ElMessage.success('密码已重置');
}

async function toggleStatus(row: UserDto) {
  const action = row.isActive ? '禁用' : '启用';
  await ElMessageBox.confirm(`确认${action}用户「${row.name}」？`, '确认', {
    type: 'warning',
  });
  await toggleUserStatusApi(row.id, !row.isActive);
  ElMessage.success(`${action}成功`);
  await loadData();
}

async function remove(row: UserDto) {
  await ElMessageBox.confirm(`确认删除用户「${row.name}」？`, '删除确认', {
    type: 'warning',
  });
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

  importing.value = true;
  try {
    const result = await validateUserImportApi(selectedImportFile.value);
    importRows.value = result.rows;
    if (result.failedCount > 0) {
      ElMessage.warning(`预览发现 ${result.failedCount} 条错误，请修正后重新选择文件`);
    } else {
      ElMessage.success(`预览通过 ${result.successCount} 条`);
    }
  } finally {
    importing.value = false;
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
  void loadData();
}

function reset() {
  query.keyword = '';
  query.page = 1;
  void loadData();
}

function flattenDepartments(nodes: DepartmentNode[], level = 0): DepartmentOption[] {
  return nodes.flatMap((node) => [
    {
      id: node.id,
      isActive: node.isActive,
      label: `${'　'.repeat(level)}${node.name}${node.isActive ? '' : '（停用）'}`,
    },
    ...flattenDepartments(node.children, level + 1),
  ]);
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
          <ElButton v-if="userActionAccess.canCreate" @click="openImport">批量导入</ElButton>
          <ElButton v-if="userActionAccess.canCreate" type="primary" @click="openCreate">新增用户</ElButton>
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
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="reset">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <div class="user-table-panel">
        <ElTable v-loading="loading" :data="users" border height="100%">
          <ElTableColumn label="工号" min-width="120" prop="employeeNo" />
          <ElTableColumn label="姓名" min-width="140" prop="name" />
          <ElTableColumn class-name="hide-on-mobile" label="部门" min-width="150" prop="departmentName" show-overflow-tooltip>
            <template #default="{ row }">
              {{ row.departmentName || '--' }}
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="邮箱" min-width="180" prop="email" />
          <ElTableColumn class-name="hide-on-mobile" label="角色" min-width="180">
            <template #default="{ row }">
              <template v-if="row.roleNames && row.roleNames.length">
                <ElTag v-for="role in row.roleNames" :key="role" size="small" style="margin-right: 4px">
                  {{ role }}
                </ElTag>
              </template>
              <span v-else class="user-empty-text">--</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="100" align="center">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'danger'" size="small">
                {{ row.isActive ? '启用' : '禁用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn fixed="right" label="操作" width="300" align="center">
            <template #default="{ row }">
              <ElButton v-if="userActionAccess.canEdit" link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
              <ElButton v-if="userActionAccess.canResetPassword" link type="primary" size="small" @click="resetPassword(row)">重置密码</ElButton>
              <ElButton
                v-if="userActionAccess.canToggleStatus"
                link
                :disabled="loading"
                :type="row.isActive ? 'danger' : 'primary'"
                size="small"
                @click="toggleStatus(row)"
              >
                {{ row.isActive ? '禁用' : '启用' }}
              </ElButton>
              <ElButton v-if="userActionAccess.canDelete" link type="danger" size="small" @click="remove(row)">删除</ElButton>
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
            <ElInput v-model="form.email" clearable placeholder="请输入邮箱" type="email" />
          </ElFormItem>
          <ElFormItem label="部门">
            <ElSelect
              v-model="form.departmentId"
              clearable
              filterable
              placeholder="选择部门"
              style="width: 100%"
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
          <ElFormItem label="角色" required>
            <ElSelect
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
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </template>
      </ElDialog>

      <ElDialog v-model="importDialogVisible" title="批量导入用户" width="920px">
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
            :disabled="!importRows.length || importRows.some((row) => !row.isValid)"
            :loading="importing"
            type="primary"
            @click="confirmImportUsers"
          >
            确认导入
          </ElButton>
        </div>
        <ElTable :data="importRows" border max-height="360">
          <ElTableColumn label="行号" prop="row" width="80" />
          <ElTableColumn label="工号" prop="employeeNo" min-width="120" />
          <ElTableColumn label="姓名" prop="name" min-width="120" />
          <ElTableColumn label="邮箱" prop="email" min-width="180" />
          <ElTableColumn label="部门名称" prop="departmentName" min-width="140" />
          <ElTableColumn label="角色名称" prop="roleName" min-width="140" />
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
  border: 1px solid var(--asset-page-border-strong);
  border-radius: 12px;
  background: var(--asset-page-surface);
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
  align-items: center;
  gap: 8px;
}

/* ========== 筛选面板 ========== */
.user-filter-panel {
  flex-shrink: 0;
  padding: 16px 20px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

.user-search-form :deep(.el-form-item) {
  margin-bottom: 0;
  margin-right: 12px;
}

.user-search-form :deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
}

/* ========== 表格面板 ========== */
.user-table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
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
  background: var(--asset-page-surface-soft);
  color: var(--asset-page-text-secondary);
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
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
  align-items: center;
  gap: 10px;
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
