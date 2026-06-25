<script lang="ts" setup>
import type { SaveTestProjectPayload, TestProjectItem } from '#/api/test-project';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

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
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  createTestProjectApi,
  deleteTestProjectApi,
  listTestProjectsApi,
  purgeTestProjectApi,
  restoreTestProjectApi,
  updateTestProjectApi,
} from '#/api/test-project';

defineOptions({ name: 'MaterialProjects' });

const { hasAccessByCodes } = useAccess();
const canManage = computed(() => hasAccessByCodes(['project:manage']));
const canPurge = computed(() => hasAccessByCodes(['material:purge']));

const loading = ref(false);
const projects = ref<TestProjectItem[]>([]);
const deleteStatus = ref<'active' | 'all' | 'deleted'>('all');

const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const saving = ref(false);
const form = reactive<SaveTestProjectPayload>({
  code: '',
  description: '',
  name: '',
});

async function loadData() {
  loading.value = true;
  try {
    projects.value = await listTestProjectsApi(deleteStatus.value);
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, { code: '', description: '', name: '' });
  dialogVisible.value = true;
}

function openEdit(row: TestProjectItem) {
  editingId.value = row.id;
  Object.assign(form, {
    code: row.code ?? '',
    description: row.description ?? '',
    name: row.name,
  });
  dialogVisible.value = true;
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('请填写项目名称');
    return;
  }
  saving.value = true;
  try {
    await (editingId.value
      ? updateTestProjectApi(editingId.value, { ...form })
      : createTestProjectApi({ ...form }));
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function remove(row: TestProjectItem) {
  await ElMessageBox.confirm(`确认删除项目「${row.name}」？`, '删除确认', {
    type: 'warning',
  });
  await deleteTestProjectApi(row.id);
  ElMessage.success('已删除');
  await loadData();
}

async function restore(row: TestProjectItem) {
  await restoreTestProjectApi(row.id);
  ElMessage.success('已恢复');
  await loadData();
}

async function purge(row: TestProjectItem) {
  await ElMessageBox.confirm(
    `彻底删除项目「${row.name}」后不可恢复，确认继续？`,
    '彻底删除确认',
    { type: 'warning' },
  );
  await purgeTestProjectApi(row.id);
  ElMessage.success('已彻底删除');
  await loadData();
}

function tableRowClassName({ row }: { row: TestProjectItem }) {
  return row.isDeleted ? 'project-row-deleted' : '';
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="p-5">
      <div class="mb-4 flex flex-wrap items-center gap-3">
        <ElSelect
          v-model="deleteStatus"
          placeholder="删除状态"
          style="width: 130px"
          @change="loadData"
        >
          <ElOption label="全部" value="all" />
          <ElOption label="未删除" value="active" />
          <ElOption label="已删除" value="deleted" />
        </ElSelect>
        <ElButton v-if="canManage" type="primary" @click="openCreate">
          新增项目
        </ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="projects"
        :row-class-name="tableRowClassName"
        border
        stripe
      >
        <ElTableColumn label="项目名称" min-width="180" prop="name" show-overflow-tooltip />
        <ElTableColumn label="项目编号" min-width="140" prop="code" />
        <ElTableColumn label="描述" min-width="200" prop="description" show-overflow-tooltip />
        <ElTableColumn align="center" label="料件数" prop="materialCount" width="90" />
        <ElTableColumn align="center" label="状态" width="100">
          <template #default="{ row }">
            <ElTag v-if="row.isDeleted" size="small" type="danger">已删除</ElTag>
            <ElTag v-else size="small" type="success">正常</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" fixed="right" label="操作" width="220">
          <template #default="{ row }">
            <template v-if="!row.isDeleted">
              <ElButton
                v-if="canManage"
                link
                size="small"
                type="primary"
                @click="openEdit(row)"
              >
                编辑
              </ElButton>
              <ElButton
                v-if="canManage"
                link
                size="small"
                type="danger"
                @click="remove(row)"
              >
                删除
              </ElButton>
            </template>
            <template v-else>
              <ElButton
                v-if="canManage"
                link
                size="small"
                type="success"
                @click="restore(row)"
              >
                撤销删除
              </ElButton>
              <ElButton
                v-if="canPurge"
                link
                size="small"
                type="danger"
                @click="purge(row)"
              >
                彻底删除
              </ElButton>
            </template>
          </template>
        </ElTableColumn>
      </ElTable>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑测试项目' : '新增测试项目'"
        width="480px"
      >
        <ElForm label-width="80px">
          <ElFormItem label="项目名称">
            <ElInput v-model="form.name" placeholder="必填" />
          </ElFormItem>
          <ElFormItem label="项目编号">
            <ElInput v-model="form.code" placeholder="可选" />
          </ElFormItem>
          <ElFormItem label="描述">
            <ElInput
              v-model="form.description"
              :rows="3"
              placeholder="可选"
              type="textarea"
            />
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
:deep(.project-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: #f3f4f6 !important;
}
</style>
