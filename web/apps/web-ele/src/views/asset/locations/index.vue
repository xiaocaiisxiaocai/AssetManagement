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
  parentId: null,
  qrCode: '',
});

async function loadData() {
  loading.value = true;
  try {
    locations.value = await getLocationTreeApi();
  } finally {
    loading.value = false;
  }
}

function openCreate(parent?: LocationNode) {
  editingId.value = null;
  Object.assign(form, {
    name: '',
    parentId: parent?.id ?? null,
    qrCode: '',
  });
  dialogVisible.value = true;
}

function openEdit(row: LocationNode) {
  editingId.value = row.id;
  Object.assign(form, {
    name: row.name,
    parentId: row.parentId ?? null,
    qrCode: row.qrCode ?? '',
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
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">存放位置</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            按仓库、区域、货架维护存放位置。
          </p>
        </div>
        <ElButton type="primary" @click="openCreate()">新增位置</ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="locations"
        row-key="id"
        border
        default-expand-all
      >
        <ElTableColumn label="位置名称" min-width="180" prop="name" />
        <ElTableColumn label="二维码" min-width="160" prop="qrCode" />
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
        :title="editingId ? '编辑位置' : '新增位置'"
        width="460px"
      >
        <ElForm label-width="88px">
          <ElFormItem label="上级 ID">
            <ElInput v-model.number="form.parentId" clearable placeholder="留空为顶级" />
          </ElFormItem>
          <ElFormItem label="位置名称">
            <ElInput v-model="form.name" />
          </ElFormItem>
          <ElFormItem label="二维码">
            <ElInput v-model="form.qrCode" />
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
