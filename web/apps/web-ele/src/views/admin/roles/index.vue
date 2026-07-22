<script lang="ts" setup>
import type { MenuDto, PermissionDto, RoleDto } from '#/api/role';

import { computed, nextTick, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  ElAlert,
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

import {
  createRoleApi,
  deleteRoleApi,
  getRoleAccessOptionsApi,
  getRoleListApi,
  setRoleAccessApi,
  updateRoleApi,
} from '#/api/role';
import { runHandled } from '#/utils/handled-promise';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { sortBuiltInMenus } from '#/utils/menu-order';
import {
  createPageSizeOptions,
  getDefaultPageSize,
} from '#/utils/runtime-settings';
import { buildRoleActionAccess } from '#/views/permissions/action-access';

import {
  collectRequiredPermissionIds,
  filterPageMenuTree,
  mergeMenuTreeSelection,
} from './menu-tree-selection';
import { buildPermissionGroups } from './permission-groups';

defineOptions({ name: 'AdminRoles' });

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

const accessForm = reactive({
  roleId: 0 as number,
  roleName: '',
  selectedPermissions: [] as number[],
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

const selectedPermissionCount = computed(
  () => accessForm.selectedPermissions.length,
);
const activePermissionGroupKeyValue = computed(
  () => activePermissionGroup.value?.key ?? '',
);
const activePermissionGroupLabel = computed(
  () => activePermissionGroup.value?.label ?? '权限',
);

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
  const selected = new Set(accessForm.selectedPermissions);
  group.permissions.forEach((perm) => selected.add(perm.id));
  accessForm.selectedPermissions = [...selected];
}

function clearCurrentModulePermissions() {
  const group = activePermissionGroup.value;
  if (!group) return;
  const ids = new Set(group.permissions.map((perm) => perm.id));
  accessForm.selectedPermissions = accessForm.selectedPermissions.filter(
    (id) => !ids.has(id) || requiredPermissionIdSet.value.has(id),
  );
}

async function loadData() {
  const requestGeneration = listRequestGuard.next();
  loading.value = true;
  try {
    const result = await getRoleListApi(
      query.keyword,
      query.page,
      query.pageSize,
    );
    if (!listRequestGuard.isLatest(requestGeneration)) return;
    roles.value = result.items;
    total.value = result.total;
  } finally {
    if (listRequestGuard.isLatest(requestGeneration)) loading.value = false;
  }
}

async function loadPermissionsAndMenus() {
  if (!canConfigureAccess.value) return;
  const options = await getRoleAccessOptionsApi();
  permissions.value = options.permissions;
  menus.value = sortBuiltInMenus(options.menus);
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
      const created = await createRoleApi({
        code: form.code,
        ...payload,
      });
      ElMessage.success('角色已创建，请继续配置授权');
      dialogVisible.value = false;
      query.page = 1;
      await loadData();
      if (canConfigureAccess.value) {
        await openAccessDialog(created);
      }
      return;
    }
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    query.page = 1;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function openAccessDialog(row: RoleDto) {
  accessForm.roleId = row.id;
  accessForm.roleName = row.name;
  accessForm.selectedPermissions = [...(row.permissionIds ?? [])];
  accessForm.selectedMenus = [...(row.menuIds ?? [])];
  permissionKeyword.value = '';
  showSelectedOnly.value = false;
  activePermissionGroupKey.value = permissionGroups.value[0]?.key ?? '';
  syncRequiredPermissions();
  accessDialogVisible.value = true;
  await nextTick();
  menuTreeRef.value?.setCheckedKeys(
    leafMenuIds(accessForm.selectedMenus),
    false,
  );
}

function syncMenuSelection() {
  const checkedKeys =
    (menuTreeRef.value?.getCheckedKeys(false) as number[]) ?? [];
  const halfCheckedKeys =
    (menuTreeRef.value?.getHalfCheckedKeys() as number[]) ?? [];
  accessForm.selectedMenus = mergeMenuTreeSelection(
    checkedKeys,
    halfCheckedKeys,
  );
  syncRequiredPermissions();
}

function syncRequiredPermissions() {
  const requiredIds = collectRequiredPermissionIds(
    pageMenuTree.value,
    permissions.value,
    accessForm.selectedMenus,
  );
  accessForm.selectedPermissions = [
    ...new Set([...accessForm.selectedPermissions, ...requiredIds]),
  ];
}

async function saveAccess() {
  syncMenuSelection();
  saving.value = true;
  try {
    await setRoleAccessApi(
      accessForm.roleId,
      accessForm.selectedPermissions,
      accessForm.selectedMenus,
    );
    ElMessage.success('角色授权已保存');
    accessDialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

function leafMenuIds(selectedIds: number[]) {
  const selected = new Set(selectedIds);
  return flattenMenus(pageMenuTree.value)
    .filter((menu) => selected.has(menu.id) && !menu.children?.length)
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
  runHandled(loadData());
}

function reset() {
  query.keyword = '';
  query.page = 1;
  runHandled(loadData());
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
        <ElButton
          v-if="roleActionAccess.canCreate"
          type="primary"
          @click="openCreate"
        >
          新增角色
        </ElButton>
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
        <ElTable :data="roles" border height="100%" v-loading="loading">
          <ElTableColumn label="角色编码" min-width="130" prop="code" />
          <ElTableColumn label="角色名称" min-width="160" prop="name" />
          <ElTableColumn align="center" label="权限数" width="100">
            <template #default="{ row }">
              {{ row.permissionIds?.length ?? 0 }}
            </template>
          </ElTableColumn>
          <ElTableColumn
            align="center"
            class-name="hide-on-mobile"
            label="菜单数"
            width="100"
          >
            <template #default="{ row }">
              {{ row.menuIds?.length ?? 0 }}
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" label="状态" width="100">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'danger'" size="small">
                {{ row.isActive ? '启用' : '禁用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" fixed="right" label="操作" width="250">
            <template #default="{ row }">
              <ElButton
                v-if="roleActionAccess.canEdit"
                link
                size="small"
                type="primary"
                @click="openEdit(row)"
              >
                编辑
              </ElButton>
              <ElButton
                v-if="canConfigureAccess"
                link
                size="small"
                type="primary"
                @click="openAccessDialog(row)"
              >
                授权配置
              </ElButton>
              <ElButton
                v-if="roleActionAccess.canDelete"
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
          <ElButton :loading="saving" type="primary" @click="save">
            保存
          </ElButton>
        </template>
      </ElDialog>

      <!-- 一体化角色授权弹窗 -->
      <ElDialog
        v-model="accessDialogVisible"
        :title="`授权配置 · ${accessForm.roleName}`"
        class="role-access-dialog"
        width="min(1180px, calc(100vw - 24px))"
      >
        <ElAlert
          :closable="false"
          class="role-access-alert"
          show-icon
          title="菜单决定用户能看到哪些入口；功能权限决定进入后能执行哪些操作。勾选菜单时会自动补齐该页面的最低访问权限。"
          type="info"
        />

        <div class="role-access-shell">
          <section class="role-menu-panel">
            <div class="role-access-section-title">
              <span>菜单范围</span>
              <span>{{ accessForm.selectedMenus.length }} 项</span>
            </div>
            <div class="role-menu-tree">
              <ElTree
                ref="menuTreeRef"
                :data="pageMenuTree"
                :props="{ children: 'children', label: 'title' }"
                default-expand-all
                node-key="id"
                show-checkbox
                @check="syncMenuSelection"
              />
            </div>
          </section>

          <section class="role-permission-panel">
            <div class="role-permission-shell">
              <aside class="role-permission-sidebar">
                <button
                  v-for="item in permissionGroups"
                  :key="item.key"
                  :class="[
                    { 'is-active': activePermissionGroupKeyValue === item.key },
                  ]"
                  :style="{ paddingLeft: `${10 + item.level * 16}px` }"
                  class="role-module-item"
                  type="button"
                  @click="selectPermissionGroup(item.key)"
                >
                  <span class="role-module-name">{{ item.label }}</span>
                  <span class="role-module-count">
                    {{ item.selected }}/{{ item.total }}
                  </span>
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
                    <ElButton @click="selectCurrentModulePermissions">
                      全选当前模块
                    </ElButton>
                    <ElButton @click="clearCurrentModulePermissions">
                      清空当前模块
                    </ElButton>
                  </div>
                </div>

                <div class="role-permission-summary">
                  <span>{{ activePermissionGroupLabel }}</span>
                  <span>
                    已选 {{ selectedPermissionCount }} /
                    {{ permissions.length }}
                  </span>
                </div>

                <ElCheckboxGroup v-model="accessForm.selectedPermissions">
                  <div
                    v-if="filteredPermissions.length > 0"
                    class="role-permission-list"
                  >
                    <ElCheckbox
                      v-for="perm in filteredPermissions"
                      :key="perm.id"
                      :disabled="requiredPermissionIdSet.has(perm.id)"
                      :label="perm.id"
                      border
                      class="role-permission-card"
                    >
                      <span class="role-permission-name">{{ perm.name }}</span>
                      <span class="role-permission-action">
                        {{
                          requiredPermissionIdSet.has(perm.id)
                            ? '菜单必需'
                            : actionLabel(perm.code)
                        }}
                      </span>
                      <span class="role-permission-code">{{ perm.code }}</span>
                    </ElCheckbox>
                  </div>
                  <ElEmpty v-else description="没有匹配的权限" />
                </ElCheckboxGroup>
              </section>
            </div>
          </section>
        </div>
        <template #footer>
          <ElButton @click="accessDialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="saveAccess">
            保存授权
          </ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
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
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border-strong);
  border-radius: 12px;
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
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  box-shadow: var(--asset-page-shadow);
}

/* ========== 表格面板 ========== */
.role-table-panel {
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

.role-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
  font-size: 14px;
  line-height: 20px;
}

.role-table-panel :deep(.el-table th.el-table__cell) {
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
  background: var(--asset-page-surface-soft);
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
.role-access-alert {
  margin-bottom: 16px;
}

.role-access-dialog {
  max-height: calc(100vh - 24px);
  margin-top: 12px;
}

.role-access-dialog :deep(.el-dialog__body) {
  max-height: calc(100vh - 164px);
  overflow-y: auto;
}

.role-access-shell {
  display: grid;
  grid-template-columns: 300px minmax(0, 1fr);
  gap: 16px;
  min-height: 540px;
}

.role-menu-panel,
.role-permission-panel {
  min-width: 0;
  overflow: hidden;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 10px;
}

.role-access-section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 46px;
  padding: 0 14px;
  font-size: 14px;
  font-weight: 600;
  color: var(--asset-page-text);
  background: var(--asset-page-surface-soft);
  border-bottom: 1px solid var(--asset-page-border);
}

.role-access-section-title span:last-child {
  font-size: 12px;
  font-weight: 400;
  color: var(--asset-page-muted);
}

.role-menu-tree {
  max-height: 520px;
  padding: 12px 10px;
  overflow-y: auto;
}

.role-permission-panel {
  padding: 10px;
}

.role-permission-shell {
  display: grid;
  grid-template-columns: 190px minmax(0, 1fr);
  gap: 12px;
  min-height: 518px;
}

.role-permission-sidebar {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 560px;
  padding: 8px;
  overflow-y: auto;
  background: var(--asset-page-surface-soft);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
}

.role-module-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 38px;
  padding: 8px 10px;
  color: var(--asset-page-text-secondary);
  cursor: pointer;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 6px;
}

.role-module-item:hover,
.role-module-item.is-active {
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary-light-5);
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
  gap: 8px;
  align-items: center;
  white-space: nowrap;
}

.role-permission-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  font-size: 13px;
  line-height: 18px;
  color: var(--asset-page-text-secondary);
  background: var(--asset-page-surface-soft);
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
}

.role-permission-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  max-height: 430px;
  padding-right: 4px;
  overflow-y: auto;
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
  align-items: center;
  width: 100%;
  min-width: 0;
}

.role-permission-name {
  min-width: 0;
  overflow: hidden;
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  color: var(--asset-page-text);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.role-permission-action {
  padding: 2px 6px;
  font-size: 12px;
  line-height: 16px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border-radius: 999px;
}

.role-permission-code {
  grid-column: 1 / -1;
  min-width: 0;
  overflow: hidden;
  font-size: 12px;
  line-height: 16px;
  color: var(--asset-page-muted);
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

/* stylelint-disable-next-line order/order -- 响应式覆盖必须位于基础规则之后 */
@media (max-width: 1024px) {
  .role-access-shell {
    grid-template-columns: minmax(0, 1fr);
    min-height: 0;
  }

  .role-menu-tree {
    max-height: 240px;
  }

  .role-permission-shell {
    min-height: 0;
  }
}

@media (max-width: 768px) {
  .role-access-dialog :deep(.el-dialog__header) {
    padding: 16px;
  }

  .role-access-dialog :deep(.el-dialog__body) {
    max-height: calc(100vh - 144px);
    padding: 16px;
  }

  .role-access-dialog :deep(.el-dialog__footer) {
    padding: 12px 16px;
  }

  .role-permission-shell {
    grid-template-columns: minmax(0, 1fr);
  }

  .role-permission-sidebar {
    flex-direction: row;
    max-height: none;
    overflow: auto hidden;
  }

  .role-module-item {
    flex: 0 0 auto;
    width: auto;
    min-height: 44px;
    padding-left: 10px !important;
  }

  .role-permission-tools {
    grid-template-columns: minmax(0, 1fr);
  }

  .role-permission-tool-actions {
    flex-wrap: wrap;
    white-space: normal;
  }

  .role-permission-list {
    grid-template-columns: minmax(0, 1fr);
    max-height: none;
  }
}

/* ========== 设计系统规范 ========== */
</style>
