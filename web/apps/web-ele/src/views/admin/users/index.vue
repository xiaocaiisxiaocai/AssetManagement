<script lang="ts" setup>
import type { UserDto } from '#/api/user';

import { onMounted, reactive, ref } from 'vue';

import {
  createUserApi,
  deleteUserApi,
  getUserListApi,
  resetUserPasswordApi,
  toggleUserStatusApi,
  updateUserApi,
} from '#/api/user';
import { getRoleListApi } from '#/api/role';

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

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const users = ref<UserDto[]>([]);
const roles = ref<any[]>([]);
const supervisorOptions = ref<UserDto[]>([]);
const total = ref(0);

const query = reactive({
  keyword: '',
  page: 1,
  pageSize: 20,
});

const form = reactive({
  employeeNo: '',
  name: '',
  email: '',
  phone: '',
  roleIds: [] as number[],
  supervisorId: undefined as number | undefined,
});

async function loadRoles() {
  const result = await getRoleListApi();
  roles.value = result.items;
}

async function loadSupervisors() {
  // 加载候选上级(用于审批流"直属主管"节点解析)
  const result = await getUserListApi('', 1, 200);
  supervisorOptions.value = result.items;
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
    phone: '',
    roleIds: [],
    supervisorId: undefined,
  });
  dialogVisible.value = true;
}

function openEdit(row: UserDto) {
  editingId.value = row.id;
  Object.assign(form, {
    employeeNo: row.employeeNo,
    name: row.name,
    email: row.email ?? '',
    phone: row.phone ?? '',
    roleIds: row.roleIds ?? [],
    supervisorId: row.supervisorId ?? undefined,
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

  saving.value = true;
  try {
    const payload = {
      name: form.name,
      email: form.email || null,
      phone: form.phone || null,
      roleIds: form.roleIds,
      supervisorId: form.supervisorId ?? null,
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
  await toggleUserStatusApi(row.id);
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
  await loadRoles();
  await loadSupervisors();
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">用户管理</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            管理用户账号、角色分配和状态控制。
          </p>
        </div>
        <ElButton type="primary" @click="openCreate">新增用户</ElButton>
      </div>

      <div class="rounded border bg-card p-4">
        <ElForm class="user-search-form" inline>
          <ElFormItem label="搜索">
            <ElInput
              v-model="query.keyword"
              clearable
              placeholder="工号或姓名"
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

        <ElTable v-loading="loading" :data="users" border>
          <ElTableColumn label="工号" min-width="120" prop="employeeNo" />
          <ElTableColumn label="姓名" min-width="140" prop="name" />
          <ElTableColumn label="邮箱" min-width="180" prop="email" />
          <ElTableColumn label="角色" min-width="180">
            <template #default="{ row }">
              <template v-if="row.roleNames && row.roleNames.length">
                <ElTag v-for="role in row.roleNames" :key="role" style="margin-right: 4px">
                  {{ role }}
                </ElTag>
              </template>
              <span v-else class="text-muted-foreground">--</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="状态" width="90">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'danger'">
                {{ row.isActive ? '启用' : '禁用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn fixed="right" label="操作" width="280">
            <template #default="{ row }">
              <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
              <ElButton link type="primary" @click="resetPassword(row)">重置密码</ElButton>
              <ElButton link :type="row.isActive ? 'danger' : 'primary'" @click="toggleStatus(row)">
                {{ row.isActive ? '禁用' : '启用' }}
              </ElButton>
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

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑用户' : '新增用户'"
        width="520px"
      >
        <ElForm label-width="100px">
          <ElFormItem label="工号">
            <ElInput
              v-model="form.employeeNo"
              :disabled="!!editingId"
              placeholder="新增用户时必填"
            />
          </ElFormItem>
          <ElFormItem label="姓名">
            <ElInput v-model="form.name" placeholder="请输入姓名" />
          </ElFormItem>
          <ElFormItem label="邮箱">
            <ElInput v-model="form.email" clearable placeholder="请输入邮箱" type="email" />
          </ElFormItem>
          <ElFormItem label="电话">
            <ElInput v-model="form.phone" clearable placeholder="请输入电话" />
          </ElFormItem>
          <ElFormItem label="角色">
            <ElSelect
              v-model="form.roleIds"
              clearable
              filterable
              multiple
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
          <ElFormItem label="直属上级">
            <ElSelect
              v-model="form.supervisorId"
              clearable
              filterable
              placeholder="选择直属上级(审批流直属主管节点据此解析)"
              style="width: 100%"
            >
              <ElOption
                v-for="u in supervisorOptions.filter((o) => o.id !== editingId)"
                :key="u.id"
                :label="`${u.name}（${u.employeeNo}）`"
                :value="u.id"
              />
            </ElSelect>
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
.user-search-form :deep(.el-form-item) {
  margin-bottom: 0;
}
</style>
