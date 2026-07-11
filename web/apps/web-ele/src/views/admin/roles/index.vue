<script lang="ts" setup>
import type { MenuDto, PermissionDto, RoleDto } from '#/api/role';

import { computed, nextTick, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  createRoleApi,
  deleteRoleApi,
  getMenusApi,
  getPermissionsApi,
  getRoleListApi,
  setRoleMenusApi,
  setRolePermissionsApi,
  updateRoleApi,
} from '#/api/role';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';
import { sortBuiltInMenus } from '#/utils/menu-order';
import { buildRoleActionAccess } from '#/views/permissions/action-access';

import { mergeMenuTreeSelection } from './menu-tree-selection';
import { buildPermissionGroups } from './permission-groups';

import {
  ElButton,
  ElCheckbox,
  ElCheckboxGroup,
  ElDialog,
  ElEmpty,
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
  ElTree,
} from 'element-plus';

defineOptions({ name: 'AdminRoles' });

const { hasAccessByCodes } = useAccess();
const roleActionAccess = computed(() => buildRoleActionAccess(hasAccessByCodes));
const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const permDialogVisible = ref(false);
const menuDialogVisible = ref(false);
const editingId = ref<null | number>(null);
const roles = ref<RoleDto[]>([]);
const permissions = ref<PermissionDto[]>([]);
const menus = ref<MenuDto[]>([]);
const total = ref(0);
const menuTreeRef = ref<InstanceType<typeof ElTree>>();
const activePermissionGroupKey = ref('');
const permissionKeyword = ref('');
const showSelectedOnly = ref(false);
const pageSizeOptions = ref(createPageSizeOptions(20));

const query = reactive({
  keyword: '',
  page: 1,
  pageSize: 20,
});

const form = reactive({
  code: '',
  name: '',
  isActive: true,
});

const permissionForm = reactive({
  roleId: 0 as number,
  selectedPermissions: [] as number[],
});

const menuForm = reactive({
  roleId: 0 as number,
  selectedMenus: [] as number[],
});

const actionLabelMap: Record<string, string> = {
  approve: '审批',
  assign: '分配',
  create: '新增',
  delete: '删除',
  design: '设计',
  edit: '编辑',
  export: '导出',
  followup: '跟进',
  handle: '处理',
  import: '导入',
  manage: '管理',
  option: '选项',
  purge: '彻底删除',
  remind: '提醒',
  restore: '恢复',
  return: '退回',
  transfer: '流转',
  upload: '上传',
  view: '查看',
};

const selectedPermissionSet = computed(() => new Set(permissionForm.selectedPermissions));

const permissionGroups = computed(() => buildPermissionGroups({
  menus: menus.value,
  permissions: permissions.value,
  selectedPermissionIds: permissionForm.selectedPermissions,
}));

const activePermissionGroup = computed(() => {
  if (!activePermissionGroupKey.value && permissionGroups.value.length > 0) {
    return permissionGroups.value[0];
  }
  return permissionGroups.value.find((item) => item.key === activePermissionGroupKey.value)
    ?? permissionGroups.value[0];
});

const filteredPermissions = computed(() => {
  const group = activePermissionGroup.value;
  if (!group) return [];

  const keyword = permissionKeyword.value.trim().toLowerCase();
  return group.permissions.filter((perm) => {
    const matchedKeyword = !keyword
      || perm.name.toLowerCase().includes(keyword)
      || perm.code.toLowerCase().includes(keyword);
    const matchedSelected = !showSelectedOnly.value || selectedPermissionSet.value.has(perm.id);
    return matchedKeyword && matchedSelected;
  });
});

const selectedPermissionCount = computed(() => permissionForm.selectedPermissions.length);
const activePermissionGroupKeyValue = computed(() => activePermissionGroup.value?.key ?? '');
const activePermissionGroupLabel = computed(() => activePermissionGroup.value?.label ?? '权限');

function actionLabel(code: string) {
  const parts = code.split(':');
  const action = parts[parts.length - 1] ?? code;
  if (action.includes('-')) {
    return action
      .split('-')
      .map((part) => actionLabelMap[part] ?? part)
      .join('/');
  }
  return actionLabelMap[action] ?? action;
}

function selectPermissionGroup(key: string) {
  activePermissionGroupKey.value = key;
}

function selectCurrentModulePermissions() {
  const group = activePermissionGroup.value;
  if (!group) return;
  const selected = new Set(permissionForm.selectedPermissions);
  group.permissions.forEach((perm) => selected.add(perm.id));
  permissionForm.selectedPermissions = [...selected];
}

function clearCurrentModulePermissions() {
  const group = activePermissionGroup.value;
  if (!group) return;
  const ids = new Set(group.permissions.map((perm) => perm.id));
  permissionForm.selectedPermissions = permissionForm.selectedPermissions.filter((id) => !ids.has(id));
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getRoleListApi(query.keyword, query.page, query.pageSize);
    roles.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

async function loadPermissionsAndMenus() {
  if (!roleActionAccess.value.canAssignPermission && !roleActionAccess.value.canAssignMenu) return;
  const [perms, menus_data] = await Promise.all([getPermissionsApi(), getMenusApi()]);
  permissions.value = perms;
  menus.value = sortBuiltInMenus(menus_data);
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    code: '',
    name: '',
    isActive: true,
  });
  dialogVisible.value = true;
}

function openEdit(row: RoleDto) {
  editingId.value = row.id;
  Object.assign(form, {
    code: row.code,
    name: row.name,
    isActive: row.isActive,
  });
  dialogVisible.value = true;
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('请填写角色名称');
    return;
  }
  if (!editingId.value && !form.code.trim()) {
    ElMessage.warning('新增角色需要填写角色编码');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      name: form.name,
      isActive: form.isActive,
    };

    if (editingId.value) {
      await updateRoleApi(editingId.value, payload);
    } else {
      await createRoleApi({
        code: form.code,
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

function openPermDialog(row: RoleDto) {
  permissionForm.roleId = row.id;
  permissionForm.selectedPermissions = [...(row.permissionIds ?? [])];
  permissionKeyword.value = '';
  showSelectedOnly.value = false;
  activePermissionGroupKey.value = permissionGroups.value[0]?.key ?? '';
  permDialogVisible.value = true;
}

async function savePermissions() {
  saving.value = true;
  try {
    await setRolePermissionsApi(permissionForm.roleId, permissionForm.selectedPermissions);
    ElMessage.success('权限分配成功');
    permDialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function openMenuDialog(row: RoleDto) {
  menuForm.roleId = row.id;
  menuForm.selectedMenus = [...(row.menuIds ?? [])];
  menuDialogVisible.value = true;
  await nextTick();
  menuTreeRef.value?.setCheckedKeys(leafMenuIds(menuForm.selectedMenus), false);
}

async function saveMenus() {
  saving.value = true;
  try {
    const checkedKeys = menuTreeRef.value?.getCheckedKeys(false) as number[] ?? [];
    const halfCheckedKeys = menuTreeRef.value?.getHalfCheckedKeys() as number[] ?? [];
    menuForm.selectedMenus = mergeMenuTreeSelection(checkedKeys, halfCheckedKeys);
    await setRoleMenusApi(menuForm.roleId, menuForm.selectedMenus);
    ElMessage.success('菜单授权成功');
    menuDialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

function leafMenuIds(selectedIds: number[]) {
  const selected = new Set(selectedIds);
  return flattenMenus(menus.value)
    .filter((menu) => selected.has(menu.id) && !(menu.children?.length))
    .map((menu) => menu.id);
}

function flattenMenus(items: MenuDto[]): MenuDto[] {
  return items.flatMap((item) => [item, ...flattenMenus(item.children ?? [])]);
}

async function remove(row: RoleDto) {
  try {
    await ElMessageBox.confirm(`确认删除角色「${row.name}」？`, '删除确认', {
      type: 'warning',
    });
  } catch {
    return;
  }
  await deleteRoleApi(row.id);
  ElMessage.success('删除成功');
  await loadData();
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

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadPermissionsAndMenus();
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="role-page">
      <div class="role-header">
        <div>
          <h2 class="role-title">角色管理</h2>
        </div>
        <ElButton v-if="roleActionAccess.canCreate" type="primary" @click="openCreate">新增角色</ElButton>
      </div>

      <div class="role-filter-panel">
        <ElForm class="filter-form" inline>
          <ElFormItem label="搜索">
            <ElInput
              v-model="query.keyword"
              clearable
              placeholder="角色名称或编码"
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

      <div class="role-table-panel">
        <ElTable v-loading="loading" :data="roles" border height="100%">
          <ElTableColumn label="角色编码" min-width="130" prop="code" />
          <ElTableColumn label="角色名称" min-width="160" prop="name" />
          <ElTableColumn label="权限数" width="100" align="center">
            <template #default="{ row }">
              {{ row.permissionIds?.length ?? 0 }}
            </template>
          </ElTableColumn>
          <ElTableColumn class-name="hide-on-mobile" label="菜单数" width="100" align="center">
            <template #default="{ row }">
              {{ row.menuIds?.length ?? 0 }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="100" align="center">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'danger'" size="small">
                {{ row.isActive ? '启用' : '禁用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn fixed="right" label="操作" width="320" align="center">
            <template #default="{ row }">
              <ElButton v-if="roleActionAccess.canEdit" link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
              <ElButton v-if="roleActionAccess.canAssignPermission" link type="primary" size="small" @click="openPermDialog(row)">权限分配</ElButton>
              <ElButton v-if="roleActionAccess.canAssignMenu" link type="primary" size="small" @click="openMenuDialog(row)">菜单授权</ElButton>
              <ElButton v-if="roleActionAccess.canDelete" link type="danger" size="small" @click="remove(row)">删除</ElButton>
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
              aria-label="角色列表每页条数"
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

      <!-- 编辑弹窗 -->
      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑角色' : '新增角色'"
        width="540px"
      >
        <ElForm class="role-edit-form" label-width="100px">
          <ElFormItem label="角色编码" required>
            <ElInput
              v-model="form.code"
              :disabled="!!editingId"
              placeholder="新增角色时必填"
            />
          </ElFormItem>
          <ElFormItem label="角色名称" required>
            <ElInput v-model="form.name" placeholder="请输入角色名称" />
          </ElFormItem>
          <ElFormItem label="启用状态">
            <ElSwitch v-model="form.isActive" />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </template>
      </ElDialog>

      <!-- 权限分配弹窗 -->
      <ElDialog v-model="permDialogVisible" title="权限分配" width="920px">
        <div class="role-permission-shell">
          <aside class="role-permission-sidebar">
            <button
              v-for="item in permissionGroups"
              :key="item.key"
              :class="[
                'role-module-item',
                { 'is-active': activePermissionGroupKeyValue === item.key },
              ]"
              :style="{ paddingLeft: `${10 + item.level * 16}px` }"
              type="button"
              @click="selectPermissionGroup(item.key)"
            >
              <span class="role-module-name">{{ item.label }}</span>
              <span class="role-module-count">{{ item.selected }}/{{ item.total }}</span>
            </button>
          </aside>

          <section class="role-permission-main">
            <div class="role-permission-tools">
              <ElInput
                v-model="permissionKeyword"
                clearable
                placeholder="搜索权限名称或编码"
              />
              <div class="role-permission-tool-actions">
                <ElCheckbox v-model="showSelectedOnly">只看已选</ElCheckbox>
                <ElButton @click="selectCurrentModulePermissions">全选当前模块</ElButton>
                <ElButton @click="clearCurrentModulePermissions">清空当前模块</ElButton>
              </div>
            </div>

            <div class="role-permission-summary">
              <span>{{ activePermissionGroupLabel }}</span>
              <span>已选 {{ selectedPermissionCount }} / {{ permissions.length }}</span>
            </div>

            <ElCheckboxGroup v-model="permissionForm.selectedPermissions">
              <div v-if="filteredPermissions.length > 0" class="role-permission-list">
                <ElCheckbox
                  v-for="perm in filteredPermissions"
                  :key="perm.id"
                  :label="perm.id"
                  class="role-permission-card"
                  border
                >
                  <span class="role-permission-name">{{ perm.name }}</span>
                  <span class="role-permission-action">{{ actionLabel(perm.code) }}</span>
                  <span class="role-permission-code">{{ perm.code }}</span>
                </ElCheckbox>
              </div>
              <ElEmpty v-else description="没有匹配的权限" />
            </ElCheckboxGroup>
          </section>
        </div>
        <template #footer>
          <ElButton @click="permDialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="savePermissions">保存</ElButton>
        </template>
      </ElDialog>

      <!-- 菜单授权弹窗 -->
      <ElDialog v-model="menuDialogVisible" title="菜单授权" width="580px">
        <ElTree
          ref="menuTreeRef"
          :data="menus"
          :props="{ children: 'children', label: 'title' }"
          show-checkbox
          node-key="id"
        />
        <template #footer>
          <ElButton @click="menuDialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="saveMenus">保存</ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
/* ========== 设计系统规范 ========== */
.role-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px;
}

/* ========== 页面头部 ========== */
.role-header {
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

.role-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
  color: var(--asset-page-text);
  letter-spacing: 0;
}

/* ========== 筛选面板 ========== */
.role-filter-panel {
  flex-shrink: 0;
  padding: 16px 20px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

/* ========== 表格面板 ========== */
.role-table-panel {
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

.role-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
  font-size: 14px;
  line-height: 20px;
}

.role-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-surface-soft);
  color: var(--asset-page-text-secondary);
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
}

.role-table-panel :deep(.el-table--border) {
  border: none;
}

.role-table-panel :deep(.el-table td.el-table__cell),
.role-table-panel :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.role-table-panel :deep(.el-table .el-table__cell) {
  padding: 12px 0;
}

.role-table-panel :deep(.el-button + .el-button) {
  margin-left: 4px;
}

/* ========== 权限分配面板 ========== */
.role-permission-shell {
  display: grid;
  grid-template-columns: 220px minmax(0, 1fr);
  gap: 16px;
  min-height: 520px;
}

.role-permission-sidebar {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px;
  max-height: 560px;
  overflow-y: auto;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  background: var(--asset-page-surface-soft);
}

.role-module-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 38px;
  padding: 8px 10px;
  border: 1px solid transparent;
  border-radius: 6px;
  color: var(--asset-page-text-secondary);
  background: transparent;
  cursor: pointer;
}

.role-module-item:hover,
.role-module-item.is-active {
  border-color: var(--el-color-primary-light-5);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.role-module-name {
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
}

.role-module-count {
  font-size: 12px;
  line-height: 18px;
  color: var(--asset-page-muted);
}

.role-permission-main {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
}

.role-permission-tools {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) auto;
  gap: 12px;
  align-items: center;
}

.role-permission-tool-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
}

.role-permission-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  color: var(--asset-page-text-secondary);
  background: var(--asset-page-surface-soft);
  font-size: 13px;
  line-height: 18px;
}

.role-permission-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  max-height: 430px;
  overflow-y: auto;
  padding-right: 4px;
}

.role-permission-card {
  width: 100%;
  min-height: 72px;
  margin-right: 0;
}

.role-permission-card :deep(.el-checkbox__label) {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 4px 8px;
  width: 100%;
  min-width: 0;
  align-items: center;
}

.role-permission-name {
  min-width: 0;
  overflow: hidden;
  color: var(--asset-page-text);
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.role-permission-action {
  padding: 2px 6px;
  border-radius: 999px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  font-size: 12px;
  line-height: 16px;
}

.role-permission-code {
  grid-column: 1 / -1;
  min-width: 0;
  overflow: hidden;
  color: var(--asset-page-muted);
  font-size: 12px;
  line-height: 16px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ========== 对话框优化 ========== */
.role-edit-form :deep(.el-form-item) {
  align-items: center;
  margin-bottom: 16px;
}

.role-edit-form :deep(.el-form-item:last-child) {
  margin-bottom: 0;
}

.role-edit-form :deep(.el-form-item__label) {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  height: 32px;
  padding-right: 12px;
  line-height: 32px;
}

.role-edit-form :deep(.el-form-item__content) {
  display: flex;
  align-items: center;
  min-height: 32px;
  line-height: 32px;
}

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

:deep(.el-input__inner) {
  font-size: 14px;
  line-height: 20px;
}

:deep(.el-textarea__inner) {
  font-size: 14px;
  line-height: 20px;
}
</style>
