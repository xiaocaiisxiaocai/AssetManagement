<script lang="ts" setup>
import type { AssetDetail, AssetItem, AssetQuery, AssetStatus } from '#/api/asset';
import type { CategoryNode, CategoryPayload, DepartmentNode, LocationNode } from '#/api/base-data';
import type { UserDto } from '#/api/user';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  deleteAssetApi,
  exportAssetsApi,
  getAssetDetailApi,
  getAssetListApi,
} from '#/api/asset';
import {
  createCategoryApi,
  deleteCategoryApi,
  getCategoryTreeApi,
  getDepartmentTreeApi,
  getLocationTreeApi,
  updateCategoryApi,
} from '#/api/base-data';
import { getWorkflowsApi } from '#/api/workflow';
import { getUserListApi } from '#/api/user';

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
  ElTabPane,
  ElTabs,
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

type FlatOption = {
  code?: string;
  id: number;
  label: string;
};

const statusOptions: Array<{ label: string; tag: 'danger' | 'info' | 'success' | 'warning'; value: AssetStatus }> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
  { label: '维修', tag: 'info', value: 2 },
  { label: '报废', tag: 'danger', value: 3 },
];

const activeView = ref<'hierarchy' | 'list'>('hierarchy');
const loading = ref(false);
const categorySaving = ref(false);
const dialogVisible = ref(false);
const categoryDialogVisible = ref(false);
const importVisible = ref(false);
const borrowDialogVisible = ref(false);
const transferDialogVisible = ref(false);
const editingAsset = ref<AssetItem | null>(null);
const formDefaultCategoryId = ref(0);
const categoryEditingId = ref<null | number>(null);
const selectedCategoryId = ref<null | number>(null);
const assets = ref<AssetItem[]>([]);
const allAssets = ref<AssetItem[]>([]);
const total = ref(0);
const categoryPath = ref<number[]>([]);
const categoryParentCode = ref('');
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
  name: '',
  page: 1,
  pageSize: 20,
  status: undefined as AssetStatus | undefined,
});

const categoryForm = reactive<CategoryPayload>({
  codeSeg: '',
  name: '',
  parentId: null,
});

const categoryOptions = computed(() => flattenCategories(categories.value));
const departmentOptions = computed(() => flattenDepartments(departments.value));
const locationOptions = computed(() => flattenLocations(locations.value));
const hierarchyContext = computed(() => getHierarchyContext());
const hierarchyNodes = computed(() => hierarchyContext.value.nodes);
const hierarchyParent = computed(() => hierarchyContext.value.parent);
const hierarchyTrail = computed(() => hierarchyContext.value.trail);
const hierarchyAssets = computed(() => {
  if (!hierarchyParent.value) return [];
  const ids = collectCategoryIds(hierarchyParent.value);
  return allAssets.value.filter((asset) => ids.includes(asset.categoryId));
});
const categoryPreviewCode = computed(() =>
  categoryParentCode.value ? `${categoryParentCode.value}-${categoryForm.codeSeg}` : categoryForm.codeSeg,
);

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
  const result = await getAssetListApi({ page: 1, pageSize: 200 });
  allAssets.value = result.items;
}

function buildQuery(): AssetQuery {
  return {
    assetNo: query.assetNo || undefined,
    categoryId: query.categoryId,
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
    categoryId: undefined,
    departmentId: undefined,
    name: '',
    page: 1,
    pageSize: 20,
    status: undefined,
  });
  selectedCategoryId.value = null;
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
  await ElMessageBox.confirm(`确认删除资产「${row.name}」？`, '删除确认', {
    type: 'warning',
  });
  await deleteAssetApi(row.id);
  ElMessage.success('删除成功');
  await Promise.all([loadData(), loadHierarchyAssets()]);
}

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
  categoryPath.value = [...categoryPath.value, node.id];
  selectedCategoryId.value = node.id;
  query.categoryId = node.id;
  query.page = 1;
  void loadData();
}

function drillToCategoryPath(index: number) {
  categoryPath.value = index < 0 ? [] : categoryPath.value.slice(0, index + 1);
  const parent = getHierarchyContext().parent;
  selectedCategoryId.value = parent?.id ?? null;
  query.categoryId = parent?.id;
  query.page = 1;
  void loadData();
}

function openCategoryCreate(parent?: CategoryNode | null) {
  const realParent = parent ?? hierarchyParent.value;
  categoryEditingId.value = null;
  categoryParentCode.value = realParent?.code ?? '';
  Object.assign(categoryForm, {
    codeSeg: '',
    name: '',
    parentId: realParent?.id ?? null,
  });
  categoryDialogVisible.value = true;
}

function openCategoryEdit(row: CategoryNode) {
  categoryEditingId.value = row.id;
  const parent = findCategoryNode(categories.value, row.parentId);
  categoryParentCode.value = parent?.code ?? '';
  Object.assign(categoryForm, {
    codeSeg: row.codeSeg,
    name: row.name,
    parentId: row.parentId ?? null,
  });
  categoryDialogVisible.value = true;
}

async function saveCategory() {
  if (!categoryForm.name.trim() || !categoryForm.codeSeg.trim()) {
    ElMessage.warning('请填写分类名称和编码段');
    return;
  }
  categorySaving.value = true;
  try {
    if (categoryEditingId.value) {
      await updateCategoryApi(categoryEditingId.value, categoryForm);
    } else {
      await createCategoryApi(categoryForm);
    }
    ElMessage.success('分类已保存');
    categoryDialogVisible.value = false;
    await loadDictionaries();
  } finally {
    categorySaving.value = false;
  }
}

async function removeCategory(row: CategoryNode) {
  await ElMessageBox.confirm(
    `确认删除分类「${row.name}」？子分类会一并删除。`,
    '删除确认',
    { type: 'warning' },
  );
  await deleteCategoryApi(row.id);
  ElMessage.success('分类已删除');
  if (categoryPath.value.includes(row.id)) {
    categoryPath.value = [];
    selectedCategoryId.value = null;
    query.categoryId = undefined;
    query.page = 1;
    await loadData();
  }
  await loadDictionaries();
}

function findCategoryNode(nodes: CategoryNode[], id?: null | number): CategoryNode | null {
  if (!id) return null;
  for (const node of nodes) {
    if (node.id === id) return node;
    const found = findCategoryNode(node.children, id);
    if (found) return found;
  }
  return null;
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

function statusMeta(status: AssetStatus) {
  return (
    statusOptions.find((item) => item.value === status) ?? {
      label: '未知',
      tag: 'info',
      value: status,
    }
  );
}

function flattenCategories(nodes: CategoryNode[], level = 0): FlatOption[] {
  return nodes.flatMap((node) => [
    { code: node.code, id: node.id, label: `${'　'.repeat(level)}${node.name}（${node.code}）` },
    ...flattenCategories(node.children, level + 1),
  ]);
}

function flattenDepartments(nodes: DepartmentNode[], level = 0): FlatOption[] {
  return nodes.flatMap((node) => [
    { id: node.id, label: `${'　'.repeat(level)}${node.name}` },
    ...flattenDepartments(node.children, level + 1),
  ]);
}

function flattenLocations(nodes: LocationNode[], level = 0): FlatOption[] {
  return nodes.flatMap((node) => [
    { id: node.id, label: `${'　'.repeat(level)}${node.name}` },
    ...flattenLocations(node.children, level + 1),
  ]);
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
  await loadDictionaries();
  await Promise.all([loadData(), loadHierarchyAssets()]);
});
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">资产列表</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            管理资产档案、分类层级、导入导出和编号生成。
          </p>
        </div>
        <div class="flex flex-wrap justify-end gap-2">
          <ElButton @click="openImport">批量导入</ElButton>
          <ElButton @click="exportAssets">导出</ElButton>
          <ElButton type="primary" @click="openCreate()">新增资产</ElButton>
        </div>
      </div>

      <div class="rounded border bg-card p-4">
        <ElTabs v-model="activeView" class="asset-view-tabs">
          <ElTabPane label="层级视图" name="hierarchy">
            <div class="space-y-4">
              <div class="flex flex-wrap items-center justify-between gap-3 rounded border border-dashed border-[var(--el-border-color)] px-3 py-2">
                <div class="asset-path">
                  <a href="#" @click.prevent="drillToCategoryPath(-1)">全部分类</a>
                  <template v-for="(node, index) in hierarchyTrail" :key="node.id">
                    <span class="text-muted-foreground">/</span>
                    <a href="#" @click.prevent="drillToCategoryPath(index)">
                      {{ node.name }}
                      <ElTag class="ml-1" size="small">{{ node.code }}</ElTag>
                    </a>
                  </template>
                </div>
                <div class="flex flex-wrap justify-end gap-2">
                  <ElButton
                    v-if="hierarchyParent"
                    @click="drillToCategoryPath(categoryPath.length - 2)"
                  >
                    返回上一层
                  </ElButton>
                  <ElButton type="primary" @click="openCategoryCreate(hierarchyParent)">
                    新增{{ hierarchyParent ? '子分类' : '分类' }}
                  </ElButton>
                  <ElButton
                    v-if="hierarchyParent"
                    plain
                    type="primary"
                    @click="openCreate(hierarchyParent.id)"
                  >
                    新增资产
                  </ElButton>
                </div>
              </div>

              <div v-if="hierarchyNodes.length" class="asset-hierarchy-grid">
                <article
                  v-for="node in hierarchyNodes"
                  :key="node.id"
                  class="asset-category-card"
                  tabindex="0"
                  @click="drillIntoCategory(node)"
                  @keydown.enter.prevent="drillIntoCategory(node)"
                >
                  <div class="flex items-start justify-between gap-3">
                    <div class="min-w-0">
                      <h3 class="truncate text-base font-semibold">{{ node.name }}</h3>
                      <ElTag class="mt-2" size="small">{{ node.code }}</ElTag>
                    </div>
                    <span class="text-sm text-[var(--el-color-primary)]">进入</span>
                  </div>

                  <div class="mt-4 grid grid-cols-2 gap-3">
                    <div class="asset-category-stat">
                      <div class="text-xs text-muted-foreground">子分类</div>
                      <div class="mt-1 text-lg font-semibold">{{ node.children.length }}</div>
                    </div>
                    <div class="asset-category-stat">
                      <div class="text-xs text-muted-foreground">资产数</div>
                      <div class="mt-1 text-lg font-semibold">{{ countCategoryAssets(node) }}</div>
                    </div>
                  </div>

                  <div class="mt-4 flex justify-end gap-2 border-t border-[var(--el-border-color)] pt-3" @click.stop>
                    <ElButton link type="primary" @click="openCategoryEdit(node)">编辑</ElButton>
                    <ElButton link type="danger" @click="removeCategory(node)">删除</ElButton>
                  </div>
                </article>
              </div>

              <div v-else class="rounded border border-dashed bg-card p-10 text-center text-muted-foreground">
                该分类下暂无子分类，可点击上方按钮新增。
              </div>

              <div v-if="hierarchyParent && hierarchyAssets.length" class="rounded border bg-card p-4">
                <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <div class="font-medium">{{ hierarchyParent.name }} 下的资产</div>
                    <div class="mt-1 text-sm text-muted-foreground">
                      {{ hierarchyParent.code }}，共 {{ hierarchyAssets.length }} 件
                    </div>
                  </div>
                  <ElButton size="small" type="primary" @click="openCreate(hierarchyParent.id)">
                    新增资产
                  </ElButton>
                </div>
                <ElTable :data="hierarchyAssets" border>
                  <ElTableColumn label="资产编号" min-width="180" prop="assetNo" />
                  <ElTableColumn label="名称" min-width="160" prop="name" />
                  <ElTableColumn label="规格型号" min-width="130" prop="model" />
                  <ElTableColumn label="归属部门" min-width="120" prop="departmentName" />
                  <ElTableColumn label="存放位置" min-width="120" prop="locationName" />
                  <ElTableColumn label="状态" width="100">
                    <template #default="{ row }">
                      <ElTag :type="statusMeta(row.status).tag">
                        {{ statusMeta(row.status).label }}
                      </ElTag>
                    </template>
                  </ElTableColumn>
                  <ElTableColumn fixed="right" label="操作" width="240">
                    <template #default="{ row }">
                      <ElButton link type="primary" @click="openDetail(row)">详情</ElButton>
                      <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
                      <ElButton link type="primary" @click="openBorrowDialog(row)">借用</ElButton>
                      <ElButton link type="primary" @click="openTransferDialog(row)">转让</ElButton>
                      <ElButton link type="danger" @click="remove(row)">删除</ElButton>
                    </template>
                  </ElTableColumn>
                </ElTable>
              </div>
            </div>
          </ElTabPane>

          <ElTabPane label="列表视图" name="list">
            <div class="space-y-4">
              <div class="rounded border bg-card p-4">
                <ElForm class="asset-filter-form" inline>
                  <ElFormItem label="资产编号">
                    <ElInput v-model="query.assetNo" clearable placeholder="请输入" />
                  </ElFormItem>
                  <ElFormItem label="资产名称">
                    <ElInput v-model="query.name" clearable placeholder="请输入" />
                  </ElFormItem>
                  <ElFormItem label="类别">
                    <ElSelect
                      v-model="query.categoryId"
                      clearable
                      filterable
                      placeholder="全部"
                      style="width: 200px"
                    >
                      <ElOption
                        v-for="item in categoryOptions"
                        :key="item.id"
                        :label="item.label"
                        :value="item.id"
                      />
                    </ElSelect>
                  </ElFormItem>
                  <ElFormItem label="状态">
                    <ElSelect v-model="query.status" clearable placeholder="全部" style="width: 140px">
                      <ElOption
                        v-for="item in statusOptions"
                        :key="item.value"
                        :label="item.label"
                        :value="item.value"
                      />
                    </ElSelect>
                  </ElFormItem>
                  <ElFormItem label="归属部门">
                    <ElSelect
                      v-model="query.departmentId"
                      clearable
                      filterable
                      placeholder="全部"
                      style="width: 180px"
                    >
                      <ElOption
                        v-for="item in departmentOptions"
                        :key="item.id"
                        :label="item.label"
                        :value="item.id"
                      />
                    </ElSelect>
                  </ElFormItem>
                  <ElFormItem>
                    <ElButton type="primary" @click="search">查询</ElButton>
                    <ElButton @click="resetQuery">重置</ElButton>
                  </ElFormItem>
                </ElForm>
              </div>

              <div class="space-y-3">
                <div class="flex flex-wrap items-center justify-between gap-3">
                  <div class="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
                    <span>共 {{ total }} 条</span>
                    <ElSelect v-model="query.pageSize" style="width: 120px" @change="search">
                      <ElOption :value="20" label="每页 20 条" />
                      <ElOption :value="50" label="每页 50 条" />
                      <ElOption :value="100" label="每页 100 条" />
                    </ElSelect>
                  </div>
                  <div class="flex flex-wrap justify-end gap-2">
                    <ElButton @click="exportAssets">批量导出</ElButton>
                  </div>
                </div>
                <ElTable v-loading="loading" :data="assets" border>
                  <ElTableColumn type="selection" width="48" />
                  <ElTableColumn label="编号" min-width="170" prop="assetNo" sortable />
                  <ElTableColumn label="名称" min-width="150" prop="name" sortable />
                  <ElTableColumn label="类别" min-width="150" prop="categoryName" />
                  <ElTableColumn label="状态" width="100">
                    <template #default="{ row }">
                      <ElTag :type="statusMeta(row.status).tag">
                        {{ statusMeta(row.status).label }}
                      </ElTag>
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="归属部门" min-width="130" prop="departmentName" />
                  <ElTableColumn label="保管人" min-width="120" prop="custodianName" />
                  <ElTableColumn label="位置" min-width="130" prop="locationName" />
                  <ElTableColumn fixed="right" label="操作" width="240">
                    <template #default="{ row }">
                      <ElButton link type="primary" @click="openDetail(row)">详情</ElButton>
                      <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
                      <ElButton link type="primary" @click="openBorrowDialog(row)">借用</ElButton>
                      <ElButton link type="primary" @click="openTransferDialog(row)">转让</ElButton>
                      <ElButton link type="danger" @click="remove(row)">删除</ElButton>
                    </template>
                  </ElTableColumn>
                </ElTable>
                <div class="flex justify-end">
                  <ElPagination
                    v-model:current-page="query.page"
                    :page-size="query.pageSize"
                    :total="total"
                    background
                    layout="prev, pager, next, jumper"
                    @current-change="loadData"
                  />
                </div>
              </div>
            </div>
          </ElTabPane>
        </ElTabs>
      </div>

      <AssetFormDialog
        v-model:visible="dialogVisible"
        :asset="editingAsset"
        :category-options="categoryOptions"
        :default-category-id="formDefaultCategoryId"
        :department-options="departmentOptions"
        :location-options="locationOptions"
        @saved="onSaved"
      />

      <AssetDetailDialog
        v-model:visible="detailVisible"
        :detail="detail"
        :loading="detailLoading"
      />

      <AssetImportDialog v-model:visible="importVisible" @imported="onImported" />

      <ElDialog
        v-model="categoryDialogVisible"
        :title="categoryEditingId ? '编辑分类' : '新增分类'"
        width="460px"
      >
        <ElForm label-width="88px">
          <ElFormItem label="上级 ID">
            <ElInput v-model.number="categoryForm.parentId" clearable placeholder="留空为顶级" />
          </ElFormItem>
          <ElFormItem label="分类名称">
            <ElInput v-model="categoryForm.name" />
          </ElFormItem>
          <ElFormItem label="编码段">
            <ElInput v-model="categoryForm.codeSeg" />
          </ElFormItem>
          <ElFormItem label="完整编码">
            <ElTag>{{ categoryPreviewCode || '待输入' }}</ElTag>
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="categoryDialogVisible = false">取消</ElButton>
          <ElButton :loading="categorySaving" type="primary" @click="saveCategory">
            保存
          </ElButton>
        </template>
      </ElDialog>

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
.asset-view-tabs :deep(.el-tabs__header) {
  margin-bottom: 16px;
}

.asset-path {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  min-height: 32px;
  font-size: 14px;
}

.asset-path a {
  color: var(--el-color-primary);
  text-decoration: none;
}

.asset-hierarchy-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 16px;
}

.asset-category-card {
  padding: 16px;
  cursor: pointer;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
  transition: border-color 0.2s ease, box-shadow 0.2s ease, transform 0.2s ease;
}

.asset-category-card:hover,
.asset-category-card:focus-visible {
  border-color: var(--el-color-primary);
  box-shadow: var(--el-box-shadow-light);
  outline: none;
  transform: translateY(-1px);
}

.asset-category-stat {
  padding: 10px 12px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
}

.asset-filter-form :deep(.el-form-item) {
  margin-bottom: 12px;
}

@media (max-width: 768px) {
  .asset-hierarchy-grid {
    grid-template-columns: 1fr;
  }
}
</style>
