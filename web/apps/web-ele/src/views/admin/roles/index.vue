<script lang="ts" setup>
import type { MenuDto, PermissionDto, RoleDto } from '#/api/role';

import { computed, onMounted, reactive, ref } from 'vue';

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

function openMenuDialog(row: RoleDto) {
  menuForm.roleId = row.id;
  menuForm.selectedMenus = [...(row.menuIds ?? [])];
  menuDialogVisible.value = true;
}

async function saveMenus() {
  saving.value = true;
  try {
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
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">角色管理</h2>
        </div>
        <ElButton type="primary" @click="openCreate">新增角色</ElButton>
      </div>

      <div class="rounded border bg-card p-4">
        <ElForm class="role-search-form" inline>
          <ElFormItem label="搜索">
            <ElInput
              v-model="query.keyword"
              clearable
              placeholder="角色名称或编码"
              @keyup.enter="search"
            />
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="reset">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <div class="space-y-3">
        <div class="flex items-center justify-between text-sm text-muted-foreground">
          <span>共 {{ total }} 条</span>
          <ElSelect v-model="query.pageSize" style="width: 140px" @change="search">
            <ElOption :value="20" label="每页 20 条" />
            <ElOption :value="50" label="每页 50 条" />
            <ElOption :value="100" label="每页 100 条" />
          </ElSelect>
        </div>

        <ElTable v-loading="loading" :data="roles" border>
          <ElTableColumn label="角色编码" min-width="130" prop="code" />
          <ElTableColumn label="角色名称" min-width="160" prop="name" />
          <ElTableColumn label="描述" min-width="200" prop="description" />
          <ElTableColumn label="权限数" width="90">
            <template #default="{ row }">
              {{ row.permissionCount ?? 0 }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="菜单数" width="90">
            <template #default="{ row }">
              {{ row.menuCount ?? 0 }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="90">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'danger'">
                {{ row.isActive ? '启用' : '禁用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn fixed="right" label="操作" width="300">
            <template #default="{ row }">
              <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
              <ElButton link type="primary" @click="openPermDialog(row)">权限分配</ElButton>
              <ElButton link type="primary" @click="openMenuDialog(row)">菜单授权</ElButton>
              <ElButton link type="danger" @click="remove(row)">删除</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>

        <div class="flex justify-end">
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
        width="520px"
      >
        <ElForm label-width="100px">
          <ElFormItem label="角色编码">
            <ElInput
              v-model="form.code"
              :disabled="!!editingId"
              placeholder="新增角色时必填"
            />
          </ElFormItem>
          <ElFormItem label="角色名称">
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
      <ElDialog v-model="permDialogVisible" title="权限分配" width="680px">
        <div class="space-y-4">
          <template v-for="[module, perms] in permissionsByModule" :key="module">
            <div class="rounded border p-3">
              <div class="mb-2 font-semibold">{{ module }}</div>
              <ElCheckboxGroup v-model="permissionForm.selectedPermissions">
                <div class="grid grid-cols-2 gap-2">
                  <ElCheckbox
                    v-for="perm in perms"
                    :key="perm.id"
                    :label="perm.id"
                    border
                  >
                    {{ perm.name }}
                    <span class="ml-1 text-xs text-muted-foreground">({{ perm.code }})</span>
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
      <ElDialog v-model="menuDialogVisible" title="菜单授权" width="560px">
        <ElTree
          v-model="menuForm.selectedMenus"
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
.role-search-form :deep(.el-form-item) {
  margin-bottom: 0;
}
</style>
