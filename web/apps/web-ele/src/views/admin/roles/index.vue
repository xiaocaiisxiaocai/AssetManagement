<script lang="ts" setup>
import type { MenuDto, PermissionDto, RoleDto } from '#/api/role';

import { computed, nextTick, onMounted, reactive, ref } from 'vue';

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

interface PermissionGroup {
  key: string;
  label: string;
  level: number;
  permissions: PermissionDto[];
  selected: number;
  total: number;
}

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

const moduleOrder = [
  'asset',
  'category',
  'location',
  'file',
  'approval',
  'report',
  'project',
  'material',
  'material-flow',
  'user',
  'department',
  'role',
  'workflow',
  'setting',
  'audit',
  'backup',
  'admin',
];

const menuPermissionModules: Record<string, string[]> = {
  Admin: ['admin', 'audit', 'backup', 'department', 'role', 'setting', 'user', 'workflow'],
  AdminAudit: ['audit'],
  AdminBackups: ['backup'],
  AdminDepartments: ['department'],
  AdminRoles: ['role'],
  AdminSettings: ['setting'],
  AdminUsers: ['user'],
  AdminWorkflows: ['workflow'],
  Approval: ['approval'],
  ApprovalMine: ['approval'],
  ApprovalPending: ['approval'],
  Asset: ['asset', 'category', 'file', 'location'],
  AssetCategories: ['category'],
  AssetList: ['asset', 'file'],
  AssetLocations: ['location'],
  ConfirmReturn: ['approval'],
  Material: ['material', 'material-flow', 'project'],
  MaterialHome: ['project'],
  MaterialProjects: ['material', 'material-flow', 'project'],
  Report: ['report'],
  ReportBorrow: ['report'],
  ReportOverdue: ['report'],
  ReportSummary: ['report'],
};

const menuPermissionCodes: Record<string, string[]> = {
  AdminAudit: ['admin:audit'],
  AdminRoles: ['admin:role'],
  AdminSettings: ['admin:setting'],
  AdminUsers: ['admin:user'],
};

const selectedPermissionSet = computed(() => new Set(permissionForm.selectedPermissions));

const permissionsByModule = computed(() => {
  const grouped: Record<string, PermissionDto[]> = {};
  permissions.value.forEach((perm) => {
    const module = perm.module || 'other';
    grouped[module] ??= [];
    grouped[module]!.push(perm);
  });

  return grouped;
});

const permissionGroups = computed(() => {
  const groups = buildMenuPermissionGroups(menus.value);
  const coveredIds = new Set(groups.flatMap((group) => group.permissions.map((perm) => perm.id)));
  const ungroupedPermissions = permissions.value
    .filter((perm) => !coveredIds.has(perm.id))
    .sort(comparePermissions);

  if (ungroupedPermissions.length > 0) {
    groups.push(toPermissionGroup({
      key: '__ungrouped__',
      label: '未挂菜单权限',
      level: 0,
      permissions: ungroupedPermissions,
    }));
  }

  return groups;
});

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

function comparePermissions(a: PermissionDto, b: PermissionDto) {
  const ai = moduleOrder.indexOf(a.module || 'other');
  const bi = moduleOrder.indexOf(b.module || 'other');
  if (ai !== bi) {
    if (ai === -1) return 1;
    if (bi === -1) return -1;
    return ai - bi;
  }
  return a.code.localeCompare(b.code);
}

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

function collectMenuPermissionModules(menu: MenuDto) {
  const modules = new Set<string>();
  const configuredModules = menuPermissionModules[menu.name] ?? [];
  configuredModules.forEach((module) => modules.add(module));

  if (menu.permissionCode) {
    const module = menu.permissionCode.split(':')[0];
    if (module) modules.add(module);
  }

  return [...modules];
}

function collectMenuPermissions(menu: MenuDto) {
  const modulePermissions = collectMenuPermissionModules(menu)
    .flatMap((module) => permissionsByModule.value[module] ?? []);
  const configuredCodePermissions = (menuPermissionCodes[menu.name] ?? [])
    .flatMap((code) => permissions.value.filter((perm) => perm.code === code));
  const codePermissions = menu.permissionCode
    ? permissions.value.filter((perm) => perm.code === menu.permissionCode)
    : [];

  return [...modulePermissions, ...configuredCodePermissions, ...codePermissions]
    .filter((perm, index, list) => list.findIndex((item) => item.id === perm.id) === index)
    .sort(comparePermissions);
}

function toPermissionGroup(group: Omit<PermissionGroup, 'selected' | 'total'>): PermissionGroup {
  return {
    ...group,
    selected: group.permissions.filter((perm) => selectedPermissionSet.value.has(perm.id)).length,
    total: group.permissions.length,
  };
}

function buildMenuPermissionGroups(menuList: MenuDto[], level = 0): PermissionGroup[] {
  const groups: PermissionGroup[] = [];

  menuList
    .filter((menu) => menu.type !== 'button')
    .forEach((menu) => {
      const children = menu.children ?? [];
      const ownPermissions = collectMenuPermissions(menu);
      const childGroups = buildMenuPermissionGroups(children, level + 1);
      const childPermissions = childGroups.flatMap((group) => group.permissions);
      const permissionsForMenu = [...ownPermissions, ...childPermissions]
        .filter((perm, index, list) => list.findIndex((item) => item.id === perm.id) === index)
        .sort(comparePermissions);

      if (permissionsForMenu.length > 0) {
        groups.push(toPermissionGroup({
          key: `menu:${menu.id}`,
          label: menu.title || menu.name,
          level,
          permissions: permissionsForMenu,
        }));
      }

      groups.push(...childGroups);
    });

  return groups;
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
  const [perms, menus_data] = await Promise.all([getPermissionsApi(), getMenusApi()]);
  permissions.value = perms;
  menus.value = menus_data;
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
  menuTreeRef.value?.setCheckedKeys(menuForm.selectedMenus, false);
}

async function saveMenus() {
  saving.value = true;
  try {
    menuForm.selectedMenus = menuTreeRef.value?.getCheckedKeys(false) as number[] ?? [];
    await setRoleMenusApi(menuForm.roleId, menuForm.selectedMenus);
    ElMessage.success('菜单授权成功');
    menuDialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function remove(row: RoleDto) {
  await ElMessageBox.confirm(`确认删除角色「${row.name}」？`, '删除确认', {
    type: 'warning',
  });
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
          <p class="role-subtitle">角色权限与菜单授权配置</p>
        </div>
        <ElButton type="primary" @click="openCreate">新增角色</ElButton>
      </div>

      <div class="role-filter-panel">
        <ElForm class="role-search-form" inline>
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
        <div class="role-table-toolbar">
          <span class="role-table-total">共 {{ total }} 条</span>
          <ElSelect
            v-model="query.pageSize"
            aria-label="角色列表每页条数"
            style="width: 140px"
            @change="search"
          >
            <ElOption :value="20" label="每页 20 条" />
            <ElOption :value="50" label="每页 50 条" />
            <ElOption :value="100" label="每页 100 条" />
          </ElSelect>
        </div>

        <ElTable v-loading="loading" :data="roles" border>
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
              <ElButton link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
              <ElButton link type="primary" size="small" @click="openPermDialog(row)">权限分配</ElButton>
              <ElButton link type="primary" size="small" @click="openMenuDialog(row)">菜单授权</ElButton>
              <ElButton link type="danger" size="small" @click="remove(row)">删除</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>

        <div class="role-pagination">
          <ElPagination
            v-model:current-page="query.page"
            :page-size="query.pageSize"
            :total="total"
            background
            layout="prev, pager, next, jumper"
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
        <ElForm label-width="100px">
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
          check-strictly
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
  min-height: calc(100vh - 112px);
}

/* ========== 页面头部 ========== */
.role-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: linear-gradient(135deg, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
  box-shadow: var(--asset-page-shadow);
}

.role-title {
  margin: 0 0 4px 0;
  font-size: 18px;
  font-weight: 600;
  line-height: 28px;
  color: var(--asset-page-text);
  letter-spacing: -0.02em;
}

.role-subtitle {
  margin: 0;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

/* ========== 筛选面板 ========== */
.role-filter-panel {
  padding: 16px 20px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

.role-search-form :deep(.el-form-item) {
  margin-bottom: 0;
  margin-right: 12px;
}

.role-search-form :deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
}

/* ========== 表格面板 ========== */
.role-table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 16px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  padding: 20px;
}

.role-table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.role-table-total {
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.role-table-panel :deep(.el-table) {
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

.role-pagination {
  display: flex;
  justify-content: flex-end;
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
