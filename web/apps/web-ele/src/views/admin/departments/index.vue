<script lang="ts" setup>
import type { DepartmentNode, DepartmentPayload } from '#/api/base-data';
import type { UserDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  createDepartmentApi,
  deleteDepartmentApi,
  getDepartmentTreeApi,
  updateDepartmentApi,
} from '#/api/base-data';
import { getUserListApi } from '#/api/user';
import { buildDepartmentActionAccess } from '#/views/permissions/action-access';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElSelect,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'AdminDepartments' });

const { hasAccessByCodes } = useAccess();
const departmentActionAccess = computed(() => buildDepartmentActionAccess(hasAccessByCodes));
const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const departments = ref<DepartmentNode[]>([]);
const userOptions = ref<UserDto[]>([]);
type DepartmentForm = Omit<DepartmentPayload, 'managerId'> & {
  managerId?: number;
};

const form = reactive<DepartmentForm>({
  isActive: true,
  managerId: undefined,
  name: '',
  parentId: null,
});

async function loadUsers() {
  const result = await getUserListApi('', 1, 500);
  userOptions.value = result.items.filter((user) => user.isActive);
}

async function loadData() {
  loading.value = true;
  try {
    departments.value = await getDepartmentTreeApi();
  } finally {
    loading.value = false;
  }
}

function openCreate(parent?: DepartmentNode) {
  editingId.value = null;
  Object.assign(form, {
    isActive: true,
    managerId: undefined,
    name: '',
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
    parentId: row.parentId ?? null,
  });
  dialogVisible.value = true;
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('请填写部门名称');
    return;
  }
  if (!form.managerId) {
    ElMessage.warning('请选择负责人');
    return;
  }
  saving.value = true;
  try {
    const payload: DepartmentPayload = {
      ...form,
      managerId: form.managerId ?? null,
    };
    if (editingId.value) {
      await updateDepartmentApi(editingId.value, payload);
    } else {
      await createDepartmentApi(payload);
    }
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function remove(row: DepartmentNode) {
  await ElMessageBox.confirm(`确认删除部门「${row.name}」？`, '删除确认', {
    type: 'warning',
  });
  await deleteDepartmentApi(row.id);
  ElMessage.success('删除成功');
  await loadData();
}

onMounted(async () => {
  await Promise.all([loadUsers(), loadData()]);
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">组织架构管理</h2>
          <p class="page-subtitle">树形组织结构与部门信息维护</p>
        </div>
        <ElButton v-if="departmentActionAccess.canCreate" type="primary" @click="openCreate()">新增部门</ElButton>
      </div>

      <div class="table-panel">
        <ElTable
          v-loading="loading"
          :data="departments"
          row-key="id"
          border
          default-expand-all
          height="100%"
        >
          <ElTableColumn label="部门名称" min-width="200" prop="name" />
          <ElTableColumn class-name="hide-on-mobile" label="负责人" min-width="140" prop="managerName" />
          <ElTableColumn class-name="hide-on-mobile" label="资产数" min-width="100" align="center" prop="assetCount" />
          <ElTableColumn label="状态" min-width="100" align="center">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
                {{ row.isActive ? '启用' : '停用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn fixed="right" label="操作" width="240" align="center">
            <template #default="{ row }">
              <ElButton v-if="departmentActionAccess.canCreate" link type="primary" size="small" @click="openCreate(row)">
                新增下级
              </ElButton>
              <ElButton v-if="departmentActionAccess.canEdit" link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
              <ElButton v-if="departmentActionAccess.canDelete" link type="danger" size="small" @click="remove(row)">删除</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑部门' : '新增部门'"
        width="500px"
      >
        <ElForm label-width="100px">
          <ElFormItem label="上级 ID">
            <ElInput v-model.number="form.parentId" clearable placeholder="留空为事业部/顶级组织" />
          </ElFormItem>
          <ElFormItem label="部门名称" required>
            <ElInput v-model="form.name" placeholder="请输入部门名称" />
          </ElFormItem>
          <ElFormItem label="负责人" required>
            <ElSelect
              v-model="form.managerId"
              filterable
              placeholder="选择该组织节点负责人"
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
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>
