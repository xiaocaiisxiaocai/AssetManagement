<script lang="ts" setup>
import type { DepartmentNode, DepartmentPayload } from '#/api/base-data';

import { onMounted, reactive, ref } from 'vue';

import {
  createDepartmentApi,
  deleteDepartmentApi,
  getDepartmentTreeApi,
  updateDepartmentApi,
} from '#/api/base-data';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElSwitch,
  ElTable,
  ElTableColumn,
} from 'element-plus';

defineOptions({ name: 'AdminDepartments' });

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const departments = ref<DepartmentNode[]>([]);
const form = reactive<DepartmentPayload>({
  isActive: true,
  name: '',
  parentId: null,
});

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
    name: '',
    parentId: parent?.id ?? null,
  });
  dialogVisible.value = true;
}

function openEdit(row: DepartmentNode) {
  editingId.value = row.id;
  Object.assign(form, {
    isActive: row.isActive,
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
  saving.value = true;
  try {
    if (editingId.value) {
      await updateDepartmentApi(editingId.value, form);
    } else {
      await createDepartmentApi(form);
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

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">组织架构</h2>
        </div>
        <ElButton type="primary" @click="openCreate()">新增部门</ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="departments"
        row-key="id"
        border
        default-expand-all
      >
        <ElTableColumn label="部门名称" min-width="180" prop="name" />
        <ElTableColumn label="负责人" min-width="120" prop="managerName" />
        <ElTableColumn label="资产数" min-width="90" prop="assetCount" />
        <ElTableColumn label="状态" min-width="90">
          <template #default="{ row }">
            {{ row.isActive ? '启用' : '停用' }}
          </template>
        </ElTableColumn>
        <ElTableColumn fixed="right" label="操作" width="220">
          <template #default="{ row }">
            <ElButton link type="primary" @click="openCreate(row)">
              新增下级
            </ElButton>
            <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
            <ElButton link type="danger" @click="remove(row)">删除</ElButton>
          </template>
        </ElTableColumn>
      </ElTable>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑部门' : '新增部门'"
        width="460px"
      >
        <ElForm label-width="88px">
          <ElFormItem label="上级 ID">
            <ElInput v-model.number="form.parentId" clearable placeholder="留空为顶级" />
          </ElFormItem>
          <ElFormItem label="部门名称">
            <ElInput v-model="form.name" />
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
