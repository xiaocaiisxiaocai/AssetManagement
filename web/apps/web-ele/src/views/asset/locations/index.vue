<script lang="ts" setup>
import type { LocationNode, LocationPayload } from '#/api/base-data';

import { onMounted, reactive, ref } from 'vue';

import {
  createLocationApi,
  deleteLocationApi,
  getLocationTreeApi,
  updateLocationApi,
} from '#/api/base-data';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
} from 'element-plus';

defineOptions({ name: 'AssetLocations' });

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const locations = ref<LocationNode[]>([]);
const form = reactive<LocationPayload>({
  name: '',
});

async function loadData() {
  loading.value = true;
  try {
    locations.value = await getLocationTreeApi();
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    name: '',
  });
  dialogVisible.value = true;
}

function openEdit(row: LocationNode) {
  editingId.value = row.id;
  Object.assign(form, {
    name: row.name,
  });
  dialogVisible.value = true;
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('请填写位置名称');
    return;
  }
  saving.value = true;
  try {
    if (editingId.value) {
      await updateLocationApi(editingId.value, form);
    } else {
      await createLocationApi(form);
    }
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function remove(row: LocationNode) {
  await ElMessageBox.confirm(`确认删除位置「${row.name}」？`, '删除确认', {
    type: 'warning',
  });
  await deleteLocationApi(row.id);
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
          <h2 class="page-title">存放位置管理</h2>
          <p class="page-subtitle">维护资产存放位置信息</p>
        </div>
        <ElButton type="primary" @click="openCreate()">新增位置</ElButton>
      </div>

      <div class="table-panel">
        <ElTable
          v-loading="loading"
          :data="locations"
          row-key="id"
          border
        >
          <ElTableColumn label="位置名称" min-width="240" prop="name" />
          <ElTableColumn fixed="right" label="操作" width="200" align="center">
            <template #default="{ row }">
              <ElButton link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
              <ElButton link type="danger" size="small" @click="remove(row)">删除</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑位置' : '新增位置'"
        width="500px"
      >
        <ElForm label-width="100px">
          <ElFormItem label="位置名称" required>
            <ElInput v-model="form.name" placeholder="请输入位置名称" />
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
