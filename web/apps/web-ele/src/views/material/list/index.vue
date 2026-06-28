<script lang="ts" setup>
import type { MaterialDetail, MaterialItem, MaterialQuery, MaterialStatus } from '#/api/material';
import type { DepartmentNode, LocationNode } from '#/api/base-data';
import type { TestProjectItem } from '#/api/test-project';
import type { UserDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  ElButton,
  ElEmpty,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { getDepartmentTreeApi, getLocationTreeApi } from '#/api/base-data';
import {
  deleteMaterialApi,
  getMaterialDetailApi,
  listMaterialsApi,
  purgeMaterialApi,
  restoreMaterialApi,
} from '#/api/material';
import { listTestProjectsApi } from '#/api/test-project';
import { getUserListApi } from '#/api/user';

import MaterialDetailDialog from '../components/MaterialDetailDialog.vue';
import MaterialFormDialog from '../components/MaterialFormDialog.vue';

defineOptions({ name: 'MaterialList' });

const { hasAccessByCodes } = useAccess();

const canCreateMaterial = computed(() => hasAccessByCodes(['material:create']));
const canEditMaterial = computed(() => hasAccessByCodes(['material:edit']));
const canDeleteMaterial = computed(() => hasAccessByCodes(['material:delete']));
const canRestoreMaterial = computed(() => hasAccessByCodes(['material:restore']));
const canPurgeMaterial = computed(() => hasAccessByCodes(['material:purge']));

type FlatOption = { id: number; label: string };

function flattenTree<T extends { id: number; name: string; children?: T[] }>(
  nodes: T[],
): FlatOption[] {
  const result: FlatOption[] = [];
  function walk(list: T[]) {
    for (const n of list) {
      result.push({ id: n.id, label: n.name });
      if (n.children?.length) walk(n.children);
    }
  }
  walk(nodes);
  return result;
}

const loading = ref(false);
const items = ref<MaterialItem[]>([]);
const total = ref(0);

const query = reactive<MaterialQuery>({
  deleteStatus: 'all',
  page: 1,
  pageSize: 20,
});

const filterProjectId = ref<number | undefined>(undefined);
const filterStatus = ref<MaterialStatus | undefined>(undefined);

const projects = ref<TestProjectItem[]>([]);
const users = ref<UserDto[]>([]);
const departmentOptions = ref<FlatOption[]>([]);
const locationOptions = ref<FlatOption[]>([]);

const materialStatusOptions: Array<{ label: string; value: MaterialStatus }> = [
  { label: '在用', value: 0 },
  { label: '已退回厂商', value: 1 },
];

const formDialogVisible = ref(false);
const editingMaterial = ref<MaterialItem | null>(null);

const detailDialogVisible = ref(false);
const detailLoading = ref(false);
const detail = ref<MaterialDetail | null>(null);

function materialRowClassName({ row }: { row: MaterialItem }) {
  if (row.isDeleted) return 'material-row-deleted';
  return row.status === 1 ? 'material-row-returned' : '';
}

function statusTag(row: MaterialItem): { label: string; type: 'danger' | 'info' | 'success' } {
  if (row.isDeleted) return { label: '已删除', type: 'danger' };
  if (row.status === 1) return { label: '已退回厂商', type: 'info' };
  return { label: '在用', type: 'success' };
}

async function loadData() {
  loading.value = true;
  try {
    const result = await listMaterialsApi(query);
    items.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

async function loadFormDeps() {
  const [deptTree, locTree, userResult, projectList] = await Promise.all([
    getDepartmentTreeApi(),
    getLocationTreeApi(),
    getUserListApi('', 1, 500),
    listTestProjectsApi('active'),
  ]);
  departmentOptions.value = flattenTree(deptTree as DepartmentNode[]);
  locationOptions.value = flattenTree(locTree as LocationNode[]);
  users.value = userResult.items.filter((u) => u.isActive);
  projects.value = projectList;
}

function search() {
  query.page = 1;
  query.projectId = filterProjectId.value ?? null;
  query.status = filterStatus.value ?? null;
  void loadData();
}

function openCreate() {
  editingMaterial.value = null;
  formDialogVisible.value = true;
  void loadFormDeps();
}

function openEdit(row: MaterialItem) {
  editingMaterial.value = row;
  formDialogVisible.value = true;
  void loadFormDeps();
}

async function openDetail(row: MaterialItem) {
  detailDialogVisible.value = true;
  detailLoading.value = true;
  detail.value = null;
  try {
    detail.value = await getMaterialDetailApi(row.id);
  } finally {
    detailLoading.value = false;
  }
}

async function remove(row: MaterialItem) {
  try {
    await ElMessageBox.confirm(
      `确认删除料件「${row.name}」？删除后仍显示在清单中，可由管理员彻底删除。`,
      '删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await deleteMaterialApi(row.id);
    ElMessage.success('已删除');
    void loadData();
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '删除失败');
  }
}

async function restore(row: MaterialItem) {
  try {
    await restoreMaterialApi(row.id);
    ElMessage.success('已恢复');
    void loadData();
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '恢复失败');
  }
}

async function purge(row: MaterialItem) {
  try {
    await ElMessageBox.confirm(
      `彻底删除料件「${row.name}」后不可恢复，确认继续？`,
      '彻底删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  try {
    await purgeMaterialApi(row.id);
    ElMessage.success('已彻底删除');
    void loadData();
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '彻底删除失败');
  }
}

onMounted(() => {
  void loadData();
});
</script>

<template>
  <re-page>
    <div class="p-5">
      <div class="mb-4 flex flex-wrap items-center gap-3">
        <ElSelect
          v-model="filterProjectId"
          clearable
          placeholder="全部项目"
          style="width: 160px"
          @change="search"
        >
          <ElOption
            v-for="p in projects"
            :key="p.id"
            :label="p.name"
            :value="p.id"
          />
        </ElSelect>
        <ElSelect
          v-model="filterStatus"
          clearable
          placeholder="全部状态"
          style="width: 130px"
          @change="search"
        >
          <ElOption
            v-for="opt in materialStatusOptions"
            :key="opt.value"
            :label="opt.label"
            :value="opt.value"
          />
        </ElSelect>
        <ElSelect
          v-model="query.deleteStatus"
          placeholder="删除状态"
          style="width: 130px"
          @change="search"
        >
          <ElOption label="全部" value="all" />
          <ElOption label="未删除" value="active" />
          <ElOption label="已删除" value="deleted" />
        </ElSelect>
        <ElButton v-if="canCreateMaterial" type="primary" @click="openCreate">
          新增料件
        </ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="items"
        :row-class-name="materialRowClassName"
        border
        style="width: 100%"
      >
        <ElTableColumn label="料件编号" min-width="150" prop="materialNo" />
        <ElTableColumn label="名称" min-width="160" prop="name" show-overflow-tooltip />
        <ElTableColumn label="所属项目" min-width="140" prop="projectName" show-overflow-tooltip />
        <ElTableColumn label="厂商" min-width="120" prop="vendorName" show-overflow-tooltip />
        <ElTableColumn align="center" label="数量" prop="quantity" width="70" />
        <ElTableColumn label="保管人" min-width="90" prop="custodianName" />
        <ElTableColumn label="位置" min-width="100" prop="locationName" show-overflow-tooltip />
        <ElTableColumn align="center" label="状态" width="110">
          <template #default="{ row }">
            <ElTag :type="statusTag(row).type" size="small">
              {{ statusTag(row).label }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" fixed="right" label="操作" width="200">
          <template #default="{ row }">
            <ElButton link size="small" type="primary" @click="openDetail(row)">详情</ElButton>
            <ElButton
              v-if="canEditMaterial && !row.isDeleted && row.status !== 1"
              link
              size="small"
              type="primary"
              @click="openEdit(row)"
            >
              编辑
            </ElButton>
            <ElButton
              v-if="canDeleteMaterial && !row.isDeleted"
              link
              size="small"
              type="danger"
              @click="remove(row)"
            >
              删除
            </ElButton>
            <ElButton
              v-if="canRestoreMaterial && row.isDeleted"
              link
              size="small"
              type="warning"
              @click="restore(row)"
            >
              撤销删除
            </ElButton>
            <ElButton
              v-if="canPurgeMaterial && row.isDeleted"
              link
              size="small"
              type="danger"
              @click="purge(row)"
            >
              彻底删除
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>

      <ElEmpty v-if="!loading && !items.length" description="暂无料件" />

      <ElPagination
        v-if="total > 0"
        v-model:current-page="query.page"
        v-model:page-size="query.pageSize"
        :page-sizes="[20, 50, 100]"
        :total="total"
        class="mt-4"
        layout="total, sizes, prev, pager, next"
        @change="loadData"
      />
    </div>

    <MaterialFormDialog
      v-model:visible="formDialogVisible"
      :department-options="departmentOptions"
      :location-options="locationOptions"
      :material="editingMaterial"
      :projects="projects"
      :users="users"
      @saved="loadData"
    />

    <MaterialDetailDialog
      v-model:visible="detailDialogVisible"
      :detail="detail"
      :loading="detailLoading"
    />
  </re-page>
</template>

<style scoped>
:deep(.material-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: var(--el-fill-color-light) !important;
}

:deep(.material-row-returned td.el-table__cell) {
  color: var(--el-text-color-secondary);
}
</style>
