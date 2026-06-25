<script lang="ts" setup>
import type {
  MaterialDetail,
  MaterialItem,
  MaterialQuery,
  MaterialStatus,
} from '#/api/material';
import type { DepartmentNode, LocationNode } from '#/api/base-data';
import type { TestProjectItem } from '#/api/test-project';
import type { UserDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import {
  ElButton,
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

import { getDepartmentTreeApi, getLocationTreeApi } from '#/api/base-data';
import {
  deleteMaterialApi,
  getMaterialDetailApi,
  listMaterialsApi,
  purgeMaterialApi,
  restoreMaterialApi,
  returnMaterialApi,
} from '#/api/material';
import { listTestProjectsApi } from '#/api/test-project';
import { getUserListApi } from '#/api/user';

import MaterialDetailDialog from '../components/MaterialDetailDialog.vue';
import MaterialFormDialog from '../components/MaterialFormDialog.vue';
import TransferDialog from '../components/TransferDialog.vue';

defineOptions({ name: 'MaterialList' });

const { hasAccessByCodes } = useAccess();

type FlatOption = { id: number; label: string };

const statusOptions: Array<{
  label: string;
  tag: 'info' | 'success';
  value: MaterialStatus;
}> = [
  { label: '在用', tag: 'success', value: 0 },
  { label: '已退回厂商', tag: 'info', value: 1 },
];

const loading = ref(false);
const materials = ref<MaterialItem[]>([]);
const total = ref(0);
const projects = ref<TestProjectItem[]>([]);
const departments = ref<DepartmentNode[]>([]);
const locations = ref<LocationNode[]>([]);
const users = ref<UserDto[]>([]);

const formVisible = ref(false);
const editingMaterial = ref<MaterialItem | null>(null);
const detailVisible = ref(false);
const detailLoading = ref(false);
const detail = ref<MaterialDetail | null>(null);
const transferVisible = ref(false);
const transferMaterial = ref<MaterialItem | null>(null);

const query = reactive({
  deleteStatus: 'all' as 'active' | 'all' | 'deleted',
  materialNo: '',
  name: '',
  page: 1,
  pageSize: 20,
  projectId: undefined as number | undefined,
  status: undefined as MaterialStatus | undefined,
});

const canCreate = computed(() => hasAccessByCodes(['material:create']));
const canEdit = computed(() => hasAccessByCodes(['material:edit']));
const canDelete = computed(() => hasAccessByCodes(['material:delete']));
const canTransfer = computed(() => hasAccessByCodes(['material:transfer']));
const canRestore = computed(() => hasAccessByCodes(['material:restore']));
const canPurge = computed(() => hasAccessByCodes(['material:purge']));

const departmentOptions = computed(() => flattenDepartments(departments.value));
const locationOptions = computed<FlatOption[]>(() =>
  locations.value.map((node) => ({ id: node.id, label: node.name })),
);

function flattenDepartments(nodes: DepartmentNode[], level = 0): FlatOption[] {
  return nodes.flatMap((node) => [
    { id: node.id, label: `${'　'.repeat(level)}${node.name}` },
    ...flattenDepartments(node.children, level + 1),
  ]);
}

function buildQuery(): MaterialQuery {
  return {
    deleteStatus: query.deleteStatus,
    materialNo: query.materialNo || undefined,
    name: query.name || undefined,
    page: query.page,
    pageSize: query.pageSize,
    projectId: query.projectId,
    status: query.status,
  };
}

async function loadDictionaries() {
  const [projectList, departmentTree, locationTree, userList] = await Promise.all([
    listTestProjectsApi('active'),
    getDepartmentTreeApi(),
    getLocationTreeApi(),
    getUserListApi().then((result) => result.items),
  ]);
  projects.value = projectList;
  departments.value = departmentTree;
  locations.value = locationTree;
  users.value = userList;
}

async function loadData() {
  loading.value = true;
  try {
    const result = await listMaterialsApi(buildQuery());
    materials.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function search() {
  query.page = 1;
  void loadData();
}

function resetQuery() {
  Object.assign(query, {
    deleteStatus: 'all',
    materialNo: '',
    name: '',
    page: 1,
    pageSize: 20,
    projectId: undefined,
    status: undefined,
  });
  void loadData();
}

function openCreate() {
  editingMaterial.value = null;
  formVisible.value = true;
}

function openEdit(row: MaterialItem) {
  editingMaterial.value = row;
  formVisible.value = true;
}

async function openDetail(row: MaterialItem) {
  detailVisible.value = true;
  detailLoading.value = true;
  detail.value = null;
  try {
    detail.value = await getMaterialDetailApi(row.id);
  } finally {
    detailLoading.value = false;
  }
}

function openTransfer(row: MaterialItem) {
  transferMaterial.value = row;
  transferVisible.value = true;
}

async function onReturn(row: MaterialItem) {
  await ElMessageBox.confirm(
    `确认将料件「${row.name}」标记为已退回厂商？`,
    '退回厂商',
    { type: 'warning' },
  );
  await returnMaterialApi(row.id);
  ElMessage.success('已标记为退回厂商');
  await loadData();
}

async function remove(row: MaterialItem) {
  await ElMessageBox.confirm(
    `确认删除料件「${row.name}」？删除后仍显示在清单中，可由管理员彻底删除。`,
    '删除确认',
    { type: 'warning' },
  );
  await deleteMaterialApi(row.id);
  ElMessage.success('已删除');
  await loadData();
}

async function restore(row: MaterialItem) {
  await ElMessageBox.confirm(`确认撤销删除料件「${row.name}」？`, '撤销删除', {
    type: 'warning',
  });
  await restoreMaterialApi(row.id);
  ElMessage.success('已恢复');
  await loadData();
}

async function purge(row: MaterialItem) {
  await ElMessageBox.confirm(
    `彻底删除料件「${row.name}」后不可恢复，确认继续？`,
    '彻底删除确认',
    { type: 'warning' },
  );
  await purgeMaterialApi(row.id);
  ElMessage.success('已彻底删除');
  await loadData();
}

function statusMeta(status: MaterialStatus) {
  return statusOptions.find((item) => item.value === status) ?? statusOptions[0]!;
}

function tableRowClassName({ row }: { row: MaterialItem }) {
  return row.isDeleted ? 'material-row-deleted' : '';
}

function onSaved() {
  void loadData();
}

onMounted(async () => {
  await Promise.all([loadDictionaries(), loadData()]);
});
</script>

<template>
  <re-page>
    <div class="material-list-page p-5">
      <div class="material-filter">
        <ElInput
          v-model="query.materialNo"
          clearable
          placeholder="料件编号"
          style="width: 180px"
          @keyup.enter="search"
        />
        <ElInput
          v-model="query.name"
          clearable
          placeholder="料件名称"
          style="width: 200px"
          @keyup.enter="search"
        />
        <ElSelect
          v-model="query.projectId"
          clearable
          filterable
          placeholder="所属项目"
          style="width: 180px"
        >
          <ElOption
            v-for="item in projects"
            :key="item.id"
            :label="item.name"
            :value="item.id"
          />
        </ElSelect>
        <ElSelect
          v-model="query.status"
          clearable
          placeholder="状态"
          style="width: 130px"
        >
          <ElOption
            v-for="item in statusOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
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
        <ElButton type="primary" @click="search">查询</ElButton>
        <ElButton @click="resetQuery">重置</ElButton>
        <ElButton v-if="canCreate" type="primary" @click="openCreate">
          新增料件
        </ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="materials"
        :row-class-name="tableRowClassName"
        border
        class="mt-4"
        stripe
      >
        <ElTableColumn label="料件编号" min-width="160" prop="materialNo" />
        <ElTableColumn
          label="名称"
          min-width="150"
          prop="name"
          show-overflow-tooltip
        />
        <ElTableColumn
          label="所属项目"
          min-width="140"
          prop="projectName"
          show-overflow-tooltip
        />
        <ElTableColumn
          label="厂商"
          min-width="120"
          prop="vendorName"
          show-overflow-tooltip
        />
        <ElTableColumn label="型号品牌" min-width="150" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.model || row.brand">{{ row.model }} {{ row.brand }}</span>
            <span v-else class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="数量" prop="quantity" width="70" />
        <ElTableColumn
          label="部门"
          min-width="110"
          prop="departmentName"
          show-overflow-tooltip
        />
        <ElTableColumn
          label="保管人"
          min-width="100"
          prop="custodianName"
          show-overflow-tooltip
        />
        <ElTableColumn align="center" label="状态" width="120">
          <template #default="{ row }">
            <ElTag :type="statusMeta(row.status).tag" size="small">
              {{ statusMeta(row.status).label }}
            </ElTag>
            <ElTag v-if="row.isDeleted" class="ml-1" size="small" type="danger">
              已删除
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" fixed="right" label="操作" width="280">
          <template #default="{ row }">
            <template v-if="!row.isDeleted">
              <ElButton link size="small" type="primary" @click="openDetail(row)">
                详情
              </ElButton>
              <ElButton
                v-if="canEdit"
                link
                size="small"
                type="primary"
                @click="openEdit(row)"
              >
                编辑
              </ElButton>
              <ElButton
                v-if="canTransfer && !row.hasPendingFlow"
                link
                size="small"
                type="info"
                @click="openTransfer(row)"
              >
                转移
              </ElButton>
              <ElButton
                v-if="canEdit && row.status === 0"
                link
                size="small"
                type="warning"
                @click="onReturn(row)"
              >
                退回厂商
              </ElButton>
              <ElButton
                v-if="canDelete"
                link
                size="small"
                type="danger"
                @click="remove(row)"
              >
                删除
              </ElButton>
            </template>
            <template v-else>
              <ElButton link size="small" type="primary" @click="openDetail(row)">
                详情
              </ElButton>
              <ElButton
                v-if="canRestore"
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

      <div class="mt-4 flex justify-end">
        <ElPagination
          v-model:current-page="query.page"
          :page-size="query.pageSize"
          :total="total"
          background
          layout="total, prev, pager, next"
          @current-change="loadData"
        />
      </div>

      <MaterialFormDialog
        v-model:visible="formVisible"
        :department-options="departmentOptions"
        :location-options="locationOptions"
        :material="editingMaterial"
        :projects="projects"
        :users="users"
        @saved="onSaved"
      />

      <MaterialDetailDialog
        v-model:visible="detailVisible"
        :detail="detail"
        :loading="detailLoading"
      />

      <TransferDialog
        v-model:visible="transferVisible"
        :material="transferMaterial"
        :users="users"
        @done="loadData"
      />
    </div>
  </re-page>
</template>

<style scoped>
.material-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
}

:deep(.material-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: #f3f4f6 !important;
}
</style>
