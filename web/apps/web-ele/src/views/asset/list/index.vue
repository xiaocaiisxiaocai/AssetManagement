<script lang="ts" setup>
import type { AssetDetail, AssetItem, AssetQuery, AssetStatus } from '#/api/asset';
import type { CategoryNode, DepartmentNode, LocationNode } from '#/api/base-data';
import type { UserDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';
import { useDebounceFn } from '@vueuse/core';

import { useAccess } from '@vben/access';

import {
  deleteAssetApi,
  exportAssetsApi,
  getAssetDetailApi,
  getAssetListApi,
  purgeAssetApi,
  restoreAssetApi,
} from '#/api/asset';
import {
  getCategoryTreeApi,
  getDepartmentTreeApi,
  getLocationTreeApi,
} from '#/api/base-data';
import { getWorkflowsApi } from '#/api/workflow';
import { getUserListApi } from '#/api/user';

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

import AssetBorrowDialog from './components/AssetBorrowDialog.vue';
import AssetDetailDialog from './components/AssetDetailDialog.vue';
import AssetFormDialog from './components/AssetFormDialog.vue';
import AssetImportDialog from './components/AssetImportDialog.vue';
import AssetTransferDialog from './components/AssetTransferDialog.vue';

defineOptions({ name: 'AssetList' });

const { hasAccessByCodes } = useAccess();

type FlatOption = {
  code?: string;
  id: number;
  label: string;
};

const statusOptions: Array<{ label: string; tag: 'danger' | 'info' | 'success' | 'warning'; value: AssetStatus }> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
];

const MAX_CATEGORY_LEVEL = 3;

const loading = ref(false);
const deletingAssetIds = ref<number[]>([]);
const dialogVisible = ref(false);
const importVisible = ref(false);
const borrowDialogVisible = ref(false);
const transferDialogVisible = ref(false);
const editingAsset = ref<AssetItem | null>(null);
const formDefaultCategoryId = ref(0);
const selectedCategoryId = ref<null | number>(null);
const assets = ref<AssetItem[]>([]);
const allAssets = ref<AssetItem[]>([]);
const total = ref(0);
const categoryPath = ref<number[]>([]);
const categoryPage = ref(1);
const categoryPageSize = ref(100);
const hierarchyKeyword = ref('');
const categories = ref<CategoryNode[]>([]);
const departments = ref<DepartmentNode[]>([]);
const locations = ref<LocationNode[]>([]);
const users = ref<UserDto[]>([]);
const workflows = ref<any[]>([]);
const currentAssetForAction = ref<AssetItem | null>(null);
const detailVisible = ref(false);
const detailLoading = ref(false);
const detail = ref<AssetDetail | null>(null);

const query = reactive({
  assetNo: '',
  categoryId: undefined as number | undefined,
  departmentId: undefined as number | undefined,
  deleteStatus: 'all' as 'active' | 'all' | 'deleted',
  name: '',
  page: 1,
  pageSize: 20,
  status: undefined as AssetStatus | undefined,
});

const categoryOptions = computed(() => flattenCategories(categories.value));
const departmentOptions = computed(() => flattenDepartments(departments.value));
const locationOptions = computed(() => flattenLocations(locations.value));
const hierarchyContext = computed(() => getHierarchyContext());
const hierarchyNodes = computed(() => hierarchyContext.value.nodes);
const hierarchyParent = computed(() => hierarchyContext.value.parent);
const hierarchyTrail = computed(() => hierarchyContext.value.trail);
const currentCategoryLevel = computed(() => categoryPath.value.length);
const isAssetStage = computed(() =>
  currentCategoryLevel.value === MAX_CATEGORY_LEVEL && !!hierarchyParent.value,
);
const isCategoryStage = computed(() => currentCategoryLevel.value < MAX_CATEGORY_LEVEL);
const currentLevelTitle = computed(() => {
  if (currentCategoryLevel.value === 0) return '一级分类';
  if (currentCategoryLevel.value === 1) return `二级分类 - ${hierarchyParent.value?.code ?? ''}`;
  if (currentCategoryLevel.value === 2) return `三级分类 - ${hierarchyParent.value?.code ?? ''}`;
  return `资产清单 - ${hierarchyParent.value?.code ?? ''}`;
});
const nextLevelName = computed(() => {
  const nextLevel = currentCategoryLevel.value + 1;
  return nextLevel === 1 ? '一级分类' : nextLevel === 2 ? '二级分类' : '三级分类';
});
const filteredHierarchyNodes = computed(() => {
  const keyword = hierarchyKeyword.value.trim().toLowerCase();
  if (!keyword) return hierarchyNodes.value;
  return hierarchyNodes.value.filter((node) =>
    `${node.code} ${node.remark ?? ''}`.toLowerCase().includes(keyword),
  );
});
const pagedHierarchyNodes = computed(() => {
  const start = (categoryPage.value - 1) * categoryPageSize.value;
  return filteredHierarchyNodes.value.slice(start, start + categoryPageSize.value);
});
const selectedCategoryAssetCount = computed(() =>
  hierarchyParent.value ? countCategoryAssets(hierarchyParent.value) : 0,
);
const assetSummaryText = computed(() => {
  if (query.deleteStatus === 'deleted') {
    return `仅显示已删除资产，检索结果 ${total.value} 条`;
  }
  if (query.deleteStatus === 'active') {
    return `仅显示未删除资产，检索结果 ${total.value} 条`;
  }
  return `当前分类共 ${selectedCategoryAssetCount.value} 件未删除资产，检索结果 ${total.value} 条(含已删除)`;
});
const canPurgeAsset = computed(() => hasAccessByCodes(['asset:purge']));
const canRestoreAsset = computed(() => hasAccessByCodes(['asset:restore']));
async function loadDictionaries() {
  const [categoryTree, departmentTree, locationTree, userList, workflowList] = await Promise.all([
    getCategoryTreeApi(),
    getDepartmentTreeApi(),
    getLocationTreeApi(),
    getUserListApi().then((result) => result.items),
    getWorkflowsApi(),
  ]);
  categories.value = categoryTree;
  departments.value = departmentTree;
  locations.value = locationTree;
  users.value = userList;
  workflows.value = workflowList;
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getAssetListApi(buildQuery());
    assets.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

async function loadHierarchyAssets() {
  const result = await getAssetListApi({ page: 1, pageSize: 1000 });
  allAssets.value = result.items;
}

function buildQuery(): AssetQuery {
  return {
    assetNo: query.assetNo || undefined,
    categoryId: query.categoryId,
    deleteStatus: query.deleteStatus,
    departmentId: query.departmentId,
    name: query.name || undefined,
    page: query.page,
    pageSize: query.pageSize,
    status: query.status,
  };
}

function resetQuery() {
  Object.assign(query, {
    assetNo: '',
    categoryId: isAssetStage.value ? hierarchyParent.value?.id : undefined,
    deleteStatus: 'all',
    departmentId: undefined,
    name: '',
    page: 1,
    pageSize: 20,
    status: undefined,
  });
  selectedCategoryId.value = isAssetStage.value ? hierarchyParent.value?.id ?? null : null;
  void loadData();
}

function search() {
  query.page = 1;
  void loadData();
}

function openCreate(categoryId?: number) {
  editingAsset.value = null;
  formDefaultCategoryId.value =
    categoryId ?? selectedCategoryId.value ?? query.categoryId ?? 0;
  dialogVisible.value = true;
}

function openEdit(row: AssetItem) {
  editingAsset.value = row;
  dialogVisible.value = true;
}

async function openDetail(row: AssetItem) {
  detailVisible.value = true;
  detailLoading.value = true;
  detail.value = null;
  try {
    detail.value = await getAssetDetailApi(row.id);
  } finally {
    detailLoading.value = false;
  }
}

function onSaved() {
  void Promise.all([loadData(), loadHierarchyAssets()]);
}

async function remove(row: AssetItem) {
  if (deletingAssetIds.value.includes(row.id)) {
    return;
  }
  await ElMessageBox.confirm(`确认删除资产「${row.name}」？删除后仍显示在清单中，可由管理员彻底删除。`, '删除确认', {
    type: 'warning',
  });
  deletingAssetIds.value = [...deletingAssetIds.value, row.id];
  try {
    await deleteAssetApi(row.id);
    if (query.deleteStatus === 'active') {
      assets.value = assets.value.filter((item) => item.id !== row.id);
      total.value = Math.max(total.value - 1, 0);
    }
    ElMessage.success('已删除');
    await Promise.all([loadData(), loadHierarchyAssets()]);
  } finally {
    deletingAssetIds.value = deletingAssetIds.value.filter((id) => id !== row.id);
  }
}

// 防抖版本的删除方法,防止用户快速点击导致重复删除
const debouncedRemove = useDebounceFn(remove, 300);

async function purge(row: AssetItem) {
  await ElMessageBox.confirm(
    `彻底删除资产「${row.name}」后不可恢复，确认继续？`,
    '彻底删除确认',
    { type: 'warning' },
  );
  await purgeAssetApi(row.id);
  ElMessage.success('已彻底删除');
  await loadData();
}

const debouncedPurge = useDebounceFn(purge, 300);

async function restore(row: AssetItem) {
  await ElMessageBox.confirm(`确认撤销删除资产「${row.name}」？将恢复为正常资产。`, '撤销删除确认', {
    type: 'warning',
  });
  await restoreAssetApi(row.id);
  ElMessage.success('已恢复');
  await Promise.all([loadData(), loadHierarchyAssets()]);
}

const debouncedRestore = useDebounceFn(restore, 300);

function getHierarchyContext() {
  let nodes = categories.value;
  let parent: CategoryNode | null = null;
  const trail: CategoryNode[] = [];
  for (const id of categoryPath.value) {
    const node = nodes.find((item) => item.id === id);
    if (!node) break;
    parent = node;
    trail.push(node);
    nodes = node.children;
  }
  return { nodes, parent, trail };
}

function collectCategoryIds(node: CategoryNode): number[] {
  return [node.id, ...node.children.flatMap((child) => collectCategoryIds(child))];
}

function countCategoryAssets(node: CategoryNode) {
  const ids = collectCategoryIds(node);
  return allAssets.value.filter((asset) => ids.includes(asset.categoryId)).length;
}

function drillIntoCategory(node: CategoryNode) {
  if (categoryPath.value.length >= MAX_CATEGORY_LEVEL) {
    return;
  }
  const nextPath = [...categoryPath.value, node.id];
  categoryPath.value = nextPath;
  selectedCategoryId.value = node.id;
  query.categoryId = nextPath.length === MAX_CATEGORY_LEVEL ? node.id : undefined;
  query.page = 1;
  hierarchyKeyword.value = '';
  categoryPage.value = 1;
  if (nextPath.length === MAX_CATEGORY_LEVEL) {
    void loadData();
  }
}

function drillToCategoryPath(index: number) {
  categoryPath.value = index < 0 ? [] : categoryPath.value.slice(0, index + 1);
  const parent = getHierarchyContext().parent;
  selectedCategoryId.value = parent?.id ?? null;
  query.categoryId = categoryPath.value.length === MAX_CATEGORY_LEVEL ? parent?.id : undefined;
  query.page = 1;
  hierarchyKeyword.value = '';
  categoryPage.value = 1;
  if (categoryPath.value.length === MAX_CATEGORY_LEVEL) {
    void loadData();
  }
}

async function exportAssets() {
  const response = await exportAssetsApi(buildQuery());
  downloadBlob(response.data, 'assets.xlsx');
}

function openImport() {
  importVisible.value = true;
}

function onImported() {
  void Promise.all([loadData(), loadHierarchyAssets()]);
}

function categoryChildLabel() {
  const level = currentCategoryLevel.value + 2;
  if (level === 2) return '二级分类';
  if (level === 3) return '三级分类';
  return '下级分类';
}

function resetCategorySearch() {
  hierarchyKeyword.value = '';
  categoryPage.value = 1;
}

function statusMeta(status: AssetStatus) {
  return (
    statusOptions.find((item) => item.value === status) ?? {
      label: '未知',
      tag: 'info',
      value: status,
    }
  );
}

function tableRowClassName({ row }: { row: AssetItem }) {
  return row.isDeleted ? 'asset-row-deleted' : '';
}

function flattenCategories(nodes: CategoryNode[], level = 0): FlatOption[] {
  return nodes.flatMap((node) => [
    { code: node.code, id: node.id, label: `${'　'.repeat(level)}${node.code}` },
    ...flattenCategories(node.children, level + 1),
  ]);
}

function flattenDepartments(nodes: DepartmentNode[], level = 0): FlatOption[] {
  return nodes.flatMap((node) => [
    { id: node.id, label: `${'　'.repeat(level)}${node.name}` },
    ...flattenDepartments(node.children, level + 1),
  ]);
}

function flattenLocations(nodes: LocationNode[]): FlatOption[] {
  return nodes.map((node) => ({ id: node.id, label: node.name }));
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

function openBorrowDialog(row: AssetItem) {
  currentAssetForAction.value = row;
  borrowDialogVisible.value = true;
}

function openTransferDialog(row: AssetItem) {
  currentAssetForAction.value = row;
  transferDialogVisible.value = true;
}

onMounted(async () => {
  await Promise.all([loadDictionaries(), loadData(), loadHierarchyAssets()]);
});
</script>

<template>
  <re-page>
    <div class="asset-list-page p-5">
      <section class="asset-workspace">
        <div class="asset-workspace-head">
          <div>
            <div class="asset-section-title">{{ currentLevelTitle }}</div>
            <div class="asset-path">
              <a href="#" @click.prevent="drillToCategoryPath(-1)">全部分类</a>
              <template v-for="(node, index) in hierarchyTrail" :key="node.id">
                <span class="text-muted-foreground">›</span>
                <a href="#" @click.prevent="drillToCategoryPath(index)">
                  {{ node.code }}
                </a>
              </template>
            </div>
          </div>
          <div class="asset-head-actions">
            <ElButton
              v-if="hierarchyParent"
              @click="drillToCategoryPath(categoryPath.length - 2)"
            >
              返回上一层
            </ElButton>
            <template v-if="isAssetStage">
              <ElButton @click="openImport">批量导入</ElButton>
              <ElButton @click="exportAssets">导出Excel</ElButton>
            </template>
            <ElButton
              v-if="!isCategoryStage"
              type="primary"
              @click="openCreate(hierarchyParent?.id)"
            >
              新增资产
            </ElButton>
          </div>
        </div>

        <template v-if="currentCategoryLevel === 0">
          <div v-if="hierarchyNodes.length" class="asset-root-grid">
            <article
              v-for="node in hierarchyNodes"
              :key="node.id"
              class="asset-root-card"
              tabindex="0"
              @click="drillIntoCategory(node)"
              @keydown.enter.prevent="drillIntoCategory(node)"
            >
              <div class="asset-root-card-code">
                <span>{{ node.codeSeg || node.code }}</span>
              </div>
              <div class="asset-root-card-body">
                <div class="asset-row-warning">
                  有 {{ node.children.length }} 个二级分类
                </div>
                <ElButton class="asset-enter-button" link type="primary" @click.stop="drillIntoCategory(node)">
                  进入
                </ElButton>
              </div>
            </article>
          </div>
          <div v-else class="asset-empty">
            暂无一级分类，请在“资产分类”页面维护分类。
          </div>
        </template>

        <template v-else-if="isCategoryStage">
          <div class="asset-filter-strip">
            <label>关键字搜索</label>
            <ElInput
              v-model="hierarchyKeyword"
              clearable
              placeholder="按编码/备注搜索"
              style="width: 240px"
              @input="categoryPage = 1"
            />
            <ElButton @click="resetCategorySearch">重置</ElButton>
          </div>

          <div v-if="filteredHierarchyNodes.length" class="asset-class-list">
            <article
              v-for="node in pagedHierarchyNodes"
              :key="node.id"
              class="asset-class-row"
              tabindex="0"
              @click="drillIntoCategory(node)"
              @keydown.enter.prevent="drillIntoCategory(node)"
            >
              <div class="asset-class-code">
                <span>{{ node.codeSeg || node.code }}</span>
              </div>
              <div class="asset-class-main">
                <div class="asset-class-name">{{ node.remark || '无备注' }}</div>
              </div>
              <div class="asset-class-actions" @click.stop>
                <span
                  v-if="currentCategoryLevel === MAX_CATEGORY_LEVEL - 1"
                  class="asset-row-warning"
                >
                  有 {{ countCategoryAssets(node) }} 条资产记录
                </span>
                <span v-else class="asset-row-warning">
                  有 {{ node.children.length }} 个{{ categoryChildLabel() }}
                </span>
                <ElButton class="asset-enter-button" link type="primary" @click.stop="drillIntoCategory(node)">
                  进入
                </ElButton>
              </div>
            </article>
          </div>
          <div v-else class="asset-empty">
            当前分类下暂无{{ nextLevelName }}，请在“资产分类”页面维护分类。
          </div>

          <div v-if="filteredHierarchyNodes.length" class="asset-pager">
            <div class="asset-pager-left">
              <span>共 {{ filteredHierarchyNodes.length }} 条记录</span>
              <span class="asset-pager-divider">|</span>
              <span>每页</span>
              <ElSelect v-model="categoryPageSize" style="width: 92px" @change="categoryPage = 1">
                <ElOption :value="10" label="10" />
                <ElOption :value="20" label="20" />
                <ElOption :value="50" label="50" />
                <ElOption :value="100" label="100" />
              </ElSelect>
            </div>
            <ElPagination
              v-model:current-page="categoryPage"
              :page-size="categoryPageSize"
              :total="filteredHierarchyNodes.length"
              background
              layout="prev, pager, next"
            />
          </div>
        </template>

        <template v-else>
          <div class="asset-filter-strip asset-filter-strip-final">
            <ElInput
              v-model="query.assetNo"
              clearable
              placeholder="资产编号"
              style="width: 200px"
              @keyup.enter="search"
            />
            <ElInput
              v-model="query.name"
              clearable
              placeholder="资产名称"
              style="width: 220px"
              @keyup.enter="search"
            />
            <ElSelect v-model="query.status" clearable placeholder="状态" style="width: 110px">
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
          </div>

          <div class="asset-table-panel">
            <div class="asset-table-summary">
              <div>
                <div class="text-sm text-muted-foreground">
                  {{ assetSummaryText }}
                </div>
              </div>
            </div>
            <ElTable
              v-loading="loading"
              :data="assets"
              :row-class-name="tableRowClassName"
              border
              stripe
            >
              <ElTableColumn label="资产编号" min-width="160" prop="assetNo" sortable />
              <ElTableColumn label="资产名称" min-width="180" prop="name" sortable show-overflow-tooltip />
              <ElTableColumn class-name="hide-on-mobile" label="归属部门" width="140" prop="departmentName" show-overflow-tooltip />
              <ElTableColumn class-name="hide-on-mobile" label="存放位置" width="140" prop="locationName" show-overflow-tooltip />
              <ElTableColumn class-name="hide-on-mobile" label="保管人" width="110" prop="custodianName" show-overflow-tooltip />
              <ElTableColumn class-name="hide-on-mobile" label="型号品牌" min-width="180" show-overflow-tooltip>
                <template #default="{ row }">
                  <span v-if="row.model || row.brand">
                    {{ row.model }} {{ row.brand }}
                  </span>
                </template>
              </ElTableColumn>
              <ElTableColumn label="数量" width="80" prop="quantity" align="center" />
              <ElTableColumn class-name="hide-on-mobile" label="照片" width="80" align="center">
                <template #default="{ row }">
                  <ElTag v-if="row.images && row.images.length > 0" size="small" type="success">
                    {{ row.images.length }}
                  </ElTag>
                  <span v-else class="text-gray-400">-</span>
                </template>
              </ElTableColumn>
              <ElTableColumn label="状态" width="90" align="center">
                <template #default="{ row }">
                  <ElTag :type="statusMeta(row.status).tag" size="small">
                    {{ statusMeta(row.status).label }}
                  </ElTag>
                  <ElTag v-if="row.isDeleted" class="ml-1" type="danger" size="small">
                    已删除
                  </ElTag>
                </template>
              </ElTableColumn>
              <ElTableColumn fixed="right" label="操作" width="240" align="center">
                <template #default="{ row }">
                  <template v-if="!row.isDeleted">
                    <ElButton link type="primary" size="small" @click="openDetail(row)">详情</ElButton>
                    <ElButton link type="primary" size="small" @click="openEdit(row)">编辑</ElButton>
                    <ElButton link type="warning" size="small" @click="openBorrowDialog(row)">借用</ElButton>
                    <ElButton link type="info" size="small" @click="openTransferDialog(row)">转让</ElButton>
                    <ElButton
                      link
                      type="danger"
                      size="small"
                      :disabled="deletingAssetIds.includes(row.id)"
                      @click="debouncedRemove(row)"
                    >
                      删除
                    </ElButton>
                  </template>
                  <template v-else>
                    <ElButton link type="primary" size="small" @click="openDetail(row)">详情</ElButton>
                    <ElButton
                      v-if="canRestoreAsset"
                      link
                      type="success"
                      size="small"
                      @click="debouncedRestore(row)"
                    >
                      撤销删除
                    </ElButton>
                    <ElButton
                      v-if="canPurgeAsset"
                      link
                      type="danger"
                      size="small"
                      @click="debouncedPurge(row)"
                    >
                      彻底删除
                    </ElButton>
                    <span v-if="!canRestoreAsset && !canPurgeAsset" class="asset-no-permission">无操作权限</span>
                  </template>
                </template>
              </ElTableColumn>
            </ElTable>
            <div class="asset-pager">
              <div class="asset-pager-left">
                <span>共 {{ total }} 条记录</span>
                <span class="asset-pager-divider">|</span>
                <span>每页</span>
                <ElSelect v-model="query.pageSize" style="width: 92px" @change="search">
                  <ElOption :value="10" label="10" />
                  <ElOption :value="20" label="20" />
                  <ElOption :value="50" label="50" />
                  <ElOption :value="100" label="100" />
                </ElSelect>
              </div>
              <ElPagination
                v-model:current-page="query.page"
                :page-size="query.pageSize"
                :total="total"
                background
                layout="prev, pager, next"
                @current-change="loadData"
              />
            </div>
          </div>
        </template>
      </section>

      <AssetFormDialog
        v-model:visible="dialogVisible"
        :asset="editingAsset"
        :category-options="categoryOptions"
        :default-category-id="formDefaultCategoryId"
        :department-options="departmentOptions"
        :location-options="locationOptions"
        :users="users"
        @saved="onSaved"
      />

      <AssetDetailDialog
        v-model:visible="detailVisible"
        :detail="detail"
        :loading="detailLoading"
      />

      <AssetImportDialog v-model:visible="importVisible" @imported="onImported" />

      <AssetBorrowDialog
        v-model:visible="borrowDialogVisible"
        :asset="currentAssetForAction"
      />

      <AssetTransferDialog
        v-model:visible="transferDialogVisible"
        :asset="currentAssetForAction"
        :users="users"
      />
    </div>
  </re-page>
</template>

<style scoped>
/* ========== 设计系统规范 ========== */
/* 间距系统: 4px 基础单位 */
/* 圆角系统: 8px(小) 12px(中) 16px(大) */
/* 字体系统: 12px(辅助) 14px(正文) 16px(小标题) 18px(标题) 20px(大标题) */
/* 颜色系统: 见下方定义 */

/* ========== 布局容器 ========== */
.asset-list-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
  min-height: calc(100vh - 112px);
}

.asset-workspace {
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 20px;
  min-height: 560px;
  padding: 20px;
  overflow: hidden;
}

.asset-workspace-head {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  padding: 16px 20px;
  border-bottom: 1px solid var(--asset-page-border);
}

/* ========== 标题与路径 ========== */
.asset-section-title {
  margin-bottom: 8px;
  font-size: 18px;
  font-weight: 600;
  color: var(--asset-page-text);
  line-height: 28px;
  letter-spacing: -0.02em;
}

.asset-path {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  min-height: 32px;
  font-size: 14px;
  line-height: 20px;
}

.asset-path a {
  color: #3b82f6;
  text-decoration: none;
  transition: color 0.2s ease;
}

.asset-path a:hover {
  color: #2563eb;
}

.asset-head-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-end;
}

/* ========== 一级分类网格 ========== */
.asset-root-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, 320px);
  gap: 20px;
  padding: 12px 0 20px;
}

.asset-root-card {
  overflow: hidden;
  cursor: pointer;
  border-radius: 12px;
  border: 1px solid var(--asset-page-border);
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.asset-root-card:hover,
.asset-root-card:focus-visible {
  border-color: #3b82f6;
  box-shadow: 0 8px 24px rgba(59, 130, 246, 0.15);
  outline: none;
  transform: translateY(-4px);
}

.asset-root-card-code {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 120px;
  color: #fff;
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  position: relative;
  overflow: hidden;
}

.asset-root-card-code::before {
  content: '';
  position: absolute;
  top: -50%;
  right: -50%;
  width: 200%;
  height: 200%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.1) 0%, transparent 70%);
  pointer-events: none;
}

.asset-root-card-code span {
  max-width: calc(100% - 48px);
  padding: 8px 20px;
  overflow: hidden;
  font-size: 20px;
  font-weight: 700;
  line-height: 28px;
  text-overflow: ellipsis;
  white-space: nowrap;
  background: rgba(255, 255, 255, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 8px;
  backdrop-filter: blur(10px);
  position: relative;
  z-index: 1;
}

.asset-root-card-body {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 16px;
  align-items: center;
  min-height: 120px;
  padding: 20px;
  background: linear-gradient(to bottom, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
}

.asset-root-actions {
  display: flex;
  gap: 8px;
  justify-content: center;
  grid-column: 1 / -1;
}

.asset-row-warning {
  font-size: 13px;
  font-weight: 500;
  line-height: 20px;
  color: #f59e0b;
  white-space: nowrap;
}

.asset-row-warning::before {
  margin-right: 4px;
  content: "●";
  color: #fbbf24;
}

.asset-enter-button {
  color: #3b82f6;
  white-space: nowrap;
  font-weight: 500;
  font-size: 14px;
  line-height: 20px;
  transition: color 0.2s ease;
}

.asset-enter-button:hover {
  color: #2563eb;
}

.asset-enter-button {
  padding: 0;
}

/* ========== 搜索栏 ========== */
.asset-filter-strip {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
  justify-content: flex-start;
  padding: 16px 20px;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: linear-gradient(135deg, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
  box-shadow: var(--asset-page-shadow);
}

.asset-filter-strip label {
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
  color: var(--asset-page-text-secondary);
}

/* ========== 分类列表 ========== */
.asset-class-list {
  display: grid;
  gap: 16px;
  overflow-y: auto;
}

.asset-class-row {
  display: grid;
  grid-template-columns: minmax(140px, 24%) 1fr auto;
  min-height: 96px;
  overflow: hidden;
  cursor: pointer;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.asset-class-row:hover,
.asset-class-row:focus-visible {
  border-color: #3b82f6;
  box-shadow: 0 8px 24px rgba(59, 130, 246, 0.15);
  outline: none;
  transform: translateY(-4px);
}

.asset-class-code {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 0;
  padding: 12px 16px;
  color: #fff;
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  position: relative;
  overflow: hidden;
}

.asset-class-code::before {
  content: '';
  position: absolute;
  top: -50%;
  right: -50%;
  width: 200%;
  height: 200%;
  background: radial-gradient(circle, rgba(255, 255, 255, 0.1) 0%, transparent 70%);
  pointer-events: none;
}

.asset-class-code span {
  max-width: 100%;
  padding: 6px 16px;
  overflow: hidden;
  font-size: 18px;
  font-weight: 700;
  line-height: 28px;
  text-overflow: ellipsis;
  white-space: nowrap;
  background: rgba(255, 255, 255, 0.2);
  border-radius: 999px;
  backdrop-filter: blur(10px);
  position: relative;
  z-index: 1;
}

.asset-class-main {
  display: flex;
  min-width: 0;
  flex-direction: column;
  justify-content: center;
  padding: 16px 24px;
}

.asset-class-name {
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
  color: var(--asset-page-text);
  white-space: pre-wrap;
  word-break: break-word;
  letter-spacing: -0.01em;
}

.asset-class-desc {
  margin-top: 8px;
  overflow: hidden;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.asset-class-actions {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: flex-end;
  min-width: 360px;
  padding: 16px 20px;
}

/* ========== 空状态 ========== */
.asset-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 320px;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
  border: 2px dashed var(--asset-page-border);
  border-radius: 12px;
  background: linear-gradient(135deg, var(--asset-page-surface-soft) 0%, var(--asset-page-surface-hover) 100%);
}

/* ========== 表格面板 ========== */
.asset-table-panel {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  background: var(--asset-page-surface);
  box-shadow: var(--asset-page-shadow);
}

.asset-table-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--asset-page-border);
  background: linear-gradient(135deg, var(--asset-page-surface) 0%, var(--asset-page-surface-soft) 100%);
}

.asset-table-panel :deep(.el-table) {
  flex: 1;
}

.asset-table-panel :deep(.el-table th.el-table__cell) {
  background: var(--asset-page-surface-soft);
  color: var(--asset-page-text-secondary);
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
}

.asset-table-panel :deep(.el-table--border) {
  border: none;
}

.asset-table-panel :deep(.el-table td.el-table__cell),
.asset-table-panel :deep(.el-table th.el-table__cell) {
  border-color: var(--asset-page-border);
}

.asset-table-panel :deep(.el-table--striped .el-table__body tr.el-table__row--striped td) {
  background: var(--asset-page-surface-soft);
}

.asset-table-panel :deep(.el-table--enable-row-hover .el-table__body tr:hover > td) {
  background-color: var(--asset-page-surface-hover) !important;
}

.asset-table-panel :deep(.asset-row-deleted td.el-table__cell) {
  color: var(--asset-page-muted);
  background-color: #f3f4f6 !important;
}

.asset-table-panel :deep(.asset-row-deleted .el-tag:not(.el-tag--danger)) {
  opacity: 0.72;
}

.asset-table-panel :deep(.el-table--enable-row-hover .el-table__body tr.asset-row-deleted:hover > td) {
  background-color: #e5e7eb !important;
}

.asset-table-panel :deep(.el-table .el-table__cell) {
  padding: 12px 0;
  font-size: 14px;
  line-height: 20px;
}

.asset-table-panel :deep(.el-button + .el-button) {
  margin-left: 4px;
}

/* ========== 分页器 ========== */
.asset-pager {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  padding: 16px 20px;
  border-top: 1px solid var(--asset-page-border);
  background: var(--asset-page-surface);
}

.asset-pager-left {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.asset-pager-divider {
  color: var(--asset-page-border);
}

/* ========== 响应式 ========== */
@media (max-width: 768px) {
  .asset-workspace-head,
  .asset-pager {
    align-items: stretch;
  }

  .asset-root-grid {
    grid-template-columns: 1fr;
  }

  .asset-class-row {
    grid-template-columns: 1fr;
  }

  .asset-class-code {
    min-height: 80px;
  }

  .asset-class-actions {
    min-width: 0;
    flex-wrap: wrap;
    justify-content: flex-start;
    padding-top: 0;
  }
}
</style>
