<script lang="ts" setup>
import type { LocationNode, LocationPayload } from '#/api/base-data';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  createLocationApi,
  deleteLocationApi,
  getLocationTreeApi,
  updateLocationApi,
} from '#/api/base-data';
import { createPageSizeOptions, getDefaultPageSize } from '#/utils/runtime-settings';
import { buildLocationActionAccess } from '#/views/permissions/action-access';

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
} from 'element-plus';

defineOptions({ name: 'AssetLocations' });

const { hasAccessByCodes } = useAccess();
const locationActionAccess = computed(() => buildLocationActionAccess(hasAccessByCodes));
const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const locations = ref<LocationNode[]>([]);
const pageSizeOptions = ref(createPageSizeOptions(20));
const query = reactive({
  page: 1,
  pageSize: 20,
});
const form = reactive<LocationPayload>({
  name: '',
});

const pagedLocations = computed(() => {
  const start = (query.page - 1) * query.pageSize;
  return locations.value.slice(start, start + query.pageSize);
});

async function loadData() {
  loading.value = true;
  try {
    locations.value = await getLocationTreeApi();
    if ((query.page - 1) * query.pageSize >= locations.value.length) {
      query.page = 1;
    }
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

function onPageSizeChange() {
  query.page = 1;
}

onMounted(async () => {
  query.pageSize = await getDefaultPageSize();
  pageSizeOptions.value = createPageSizeOptions(query.pageSize);
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">存放位置管理</h2>
          <p class="page-subtitle">维护资产存放位置信息</p>
        </div>
        <ElButton v-if="locationActionAccess.canCreate" type="primary" @click="openCreate()">新增位置</ElButton>
      </div>

      <div class="table-panel">
        <ElTable
          v-loading="loading"
          :data="pagedLocations"
          row-key="id"
          border
          height="100%"
        >
          <ElTableColumn label="位置名称" min-width="240" prop="name" />
          <ElTableColumn v-if="locationActionAccess.canEdit || locationActionAccess.canDelete" fixed="right" label="操作" width="200" align="center">
            <template #default="{ row }">
              <ElButton v-if="locationActionAccess.canEdit" link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
              <ElButton v-if="locationActionAccess.canDelete" link type="danger" size="small" @click="remove(row)">删除</ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
        <div class="table-bottom-pager">
          <div class="table-bottom-pager-left">
            <span>共 {{ locations.length }} 条记录</span>
            <span class="table-bottom-pager-divider">|</span>
            <span>每页</span>
            <ElSelect v-model="query.pageSize" style="width: 92px" @change="onPageSizeChange">
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
            :total="locations.length"
            background
            layout="prev, pager, next"
          />
        </div>
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
