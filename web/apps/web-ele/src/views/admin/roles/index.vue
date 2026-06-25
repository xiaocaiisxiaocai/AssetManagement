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

const query = reactive({
  keyword: '',
  page: 1,
  pageSize: 20,
});

const form = reactive({
  code: '',
  name: '',
  description: '',
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

const permissionsByModule = computed(() => {
  const grouped: Record<string, PermissionDto[]> = {};
  permissions.value.forEach((perm) => {
    if (!grouped[perm.module]) {
      grouped[perm.module] = [];
    }
    grouped[perm.module]!.push(perm);
  });
  return Object.entries(grouped).sort(([a], [b]) => a.localeCompare(b));
});

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
    description: '',
    isActive: true,
  });
  dialogVisible.value = true;
}

function openEdit(row: RoleDto) {
  editingId.value = row.id;
  Object.assign(form, {
    code: row.code,
    name: row.name,
    description: row.description ?? '',
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
      description: form.description || null,
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

      <div class="filter-panel">
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
          <ElTableColumn class-name="hide-on-mobile" label="描述" min-width="200" prop="description" />
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
          <ElFormItem label="描述">
            <ElInput
              v-model="form.description"
              clearable
              placeholder="请输入角色描述"
              :rows="3"
              type="textarea"
            />
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
      <ElDialog v-model="permDialogVisible" title="权限分配" width="720px">
        <div class="role-permission-panel">
          <template v-for="[module, perms] in permissionsByModule" :key="module">
            <div class="role-permission-group">
              <div class="role-permission-group-title">{{ module }}</div>
              <ElCheckboxGroup v-model="permissionForm.selectedPermissions">
                <div class="role-permission-grid">
                  <ElCheckbox
                    v-for="perm in perms"
                    :key="perm.id"
                    :label="perm.id"
                    border
                  >
                    {{ perm.name }}
                    <span class="role-permission-code">({{ perm.code }})</span>
                  </ElCheckbox>
                </div>
              </ElCheckboxGroup>
            </div>
          </template>
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
.role-permission-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-height: 500px;
  overflow-y: auto;
}

.role-permission-group {
  padding: 16px;
  border: 1px solid var(--asset-page-border);
  border-radius: 8px;
  background: var(--asset-page-surface-soft);
}

.role-permission-group-title {
  margin-bottom: 12px;
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  color: var(--asset-page-text);
}

.role-permission-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
}

.role-permission-code {
  margin-left: 4px;
  font-size: 12px;
  line-height: 16px;
  color: var(--asset-page-muted);
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
