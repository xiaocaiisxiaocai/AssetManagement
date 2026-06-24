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
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">组织架构管理</h2>
          <p class="page-subtitle">树形组织结构与部门信息维护</p>
        </div>
        <ElButton type="primary" @click="openCreate()">新增部门</ElButton>
      </div>

      <div class="table-panel">
        <ElTable
          v-loading="loading"
          :data="departments"
          row-key="id"
          border
          default-expand-all
        >
          <ElTableColumn label="部门名称" min-width="200" prop="name" />
          <ElTableColumn label="负责人" min-width="140" prop="managerName" />
          <ElTableColumn label="资产数" min-width="100" align="center" prop="assetCount" />
          <ElTableColumn label="状态" min-width="100" align="center">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
                {{ row.isActive ? '启用' : '停用' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn fixed="right" label="操作" width="240" align="center">
            <template #default="{ row }">
              <ElButton link type="primary" size="small" @click="openCreate(row)">
                新增下级
              </ElButton>
              <ElButton link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
              <ElButton link type="danger" size="small" @click="remove(row)">删除</ElButton>
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
            <ElInput v-model.number="form.parentId" clearable placeholder="留空为顶级部门" />
          </ElFormItem>
          <ElFormItem label="部门名称" required>
            <ElInput v-model="form.name" placeholder="请输入部门名称" />
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
