<script lang="ts" setup>
import type { AssetItem, AssetPayload, AssetQuery, AssetStatus, ImportPreviewRow } from '#/api/asset';
import type { CategoryNode, CategoryPayload, DepartmentNode, LocationNode } from '#/api/base-data';
import type { UserDto } from '#/api/user';
import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  confirmAssetImportApi,
  createAssetApi,
  deleteAssetApi,
  downloadAssetTemplateApi,
  exportAssetsApi,
  getAssetListApi,
  updateAssetApi,
  uploadAssetImageApi,
  validateAssetImportApi,
} from '#/api/asset';
import {
  createCategoryApi,
  deleteCategoryApi,
  getCategoryTreeApi,
  getDepartmentTreeApi,
  getLocationTreeApi,
  updateCategoryApi,
} from '#/api/base-data';
import { startApprovalApi, getWorkflowsApi } from '#/api/workflow';
import { getUserListApi } from '#/api/user';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
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
  ElUpload,
} from 'element-plus';

defineOptions({ name: 'AssetList' });

type FlatOption = {
  code?: string;
  id: number;
  label: string;
};

type AssetFormState = {
  brand?: string;
  categoryId: number;
  departmentId?: number;
  locationId?: number;
  model?: string;
  name: string;
  price: number;
  quantity: number;
  status: AssetStatus;
};

const statusOptions: Array<{ label: string; tag: 'danger' | 'info' | 'success' | 'warning'; value: AssetStatus }> = [
  { label: '在库', tag: 'success', value: 0 },
  { label: '借出', tag: 'warning', value: 1 },
  { label: '维修', tag: 'info', value: 2 },
  { label: '报废', tag: 'danger', value: 3 },
];

const activeView = ref<'hierarchy' | 'list'>('hierarchy');
const loading = ref(false);
const saving = ref(false);
const categorySaving = ref(false);
const importing = ref(false);
const dialogVisible = ref(false);
const categoryDialogVisible = ref(false);
const importVisible = ref(false);
const borrowDialogVisible = ref(false);
const transferDialogVisible = ref(false);
const editingId = ref<null | number>(null);
const categoryEditingId = ref<null | number>(null);
const selectedCategoryId = ref<null | number>(null);
const selectedFile = ref<File | null>(null);
const importPreview = ref<ImportPreviewRow[]>([]);
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
const imageFileList = ref<UploadUserFile[]>([]);

const query = reactive({
  assetNo: '',
  categoryId: undefined as number | undefined,
  departmentId: undefined as number | undefined,
  name: '',
  page: 1,
  pageSize: 20,
  status: undefined as AssetStatus | undefined,
});

const form = reactive<AssetFormState>({
  brand: '',
  categoryId: 0,
  departmentId: undefined,
  locationId: undefined,
  model: '',
  name: '',
  price: 0,
  quantity: 1,
  status: 0,
});
const categoryForm = reactive<CategoryPayload>({
  codeSeg: '',
  name: '',
  parentId: null,
});

const borrowForm = reactive({
  returnDate: '' as string | Date,
  reason: '',
});

const transferForm = reactive({
  transfereeId: undefined as number | undefined,
  reason: '',
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
const selectedCategory = computed(() =>
  categoryOptions.value.find((item) => item.id === form.categoryId),
);
const assetNoPreview = computed(() =>
  selectedCategory.value?.code ? `${selectedCategory.value.code}-自动流水` : '选择分类后生成',
);
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
  editingId.value = null;
  Object.assign(form, {
    brand: '',
    categoryId: categoryId ?? selectedCategoryId.value ?? query.categoryId ?? 0,
    departmentId: undefined,
    locationId: undefined,
    model: '',
    name: '',
    price: 0,
    quantity: 1,
    status: 0,
  });
  imageFileList.value = [];
  dialogVisible.value = true;
}

function openEdit(row: AssetItem) {
  editingId.value = row.id;
  Object.assign(form, {
    brand: row.brand ?? '',
    categoryId: row.categoryId,
    departmentId: row.departmentId ?? undefined,
    locationId: row.locationId ?? undefined,
    model: row.model ?? '',
    name: row.name,
    price: row.price,
    quantity: row.quantity,
    status: row.status,
  });
  imageFileList.value = (row.images ?? []).map((url, index) => ({
    name: url.split('/').pop() ?? url,
    status: 'success',
    uid: -(index + 1),
    url,
  }));
  dialogVisible.value = true;
}

async function save() {
  if (!form.name.trim() || !form.categoryId) {
    ElMessage.warning('请填写资产名称并选择分类');
    return;
  }
  saving.value = true;
  try {
    if (editingId.value) {
      await updateAssetApi(editingId.value, buildPayload());
    } else {
      await createAssetApi(buildPayload());
    }
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await Promise.all([loadData(), loadHierarchyAssets()]);
  } finally {
    saving.value = false;
  }
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

async function downloadTemplate() {
  const response = await downloadAssetTemplateApi();
  downloadBlob(response.data, 'asset-import-template.xlsx');
}

async function exportAssets() {
  const response = await exportAssetsApi(buildQuery());
  downloadBlob(response.data, 'assets.xlsx');
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  selectedFile.value = input.files?.[0] ?? null;
  importPreview.value = [];
}

async function validateImport() {
  if (!selectedFile.value) {
    ElMessage.warning('请先选择 Excel 文件');
    return;
  }
  importing.value = true;
  try {
    importPreview.value = await validateAssetImportApi(selectedFile.value);
  } finally {
    importing.value = false;
  }
}

async function confirmImport() {
  if (!selectedFile.value) {
    ElMessage.warning('请先选择 Excel 文件');
    return;
  }
  importing.value = true;
  try {
    const result = await confirmAssetImportApi(selectedFile.value);
    importPreview.value = result.rows;
    ElMessage.success(`导入成功 ${result.successCount} 条，失败 ${result.failedCount} 条`);
    await Promise.all([loadData(), loadHierarchyAssets()]);
  } finally {
    importing.value = false;
  }
}

function openImport() {
  selectedFile.value = null;
  importPreview.value = [];
  importVisible.value = true;
}

function buildPayload(): AssetPayload {
  return {
    brand: form.brand,
    categoryId: form.categoryId,
    departmentId: form.departmentId,
    images: imageFileList.value
      .map((f) => f.url ?? (f.response as { url?: string } | undefined)?.url)
      .filter((u): u is string => !!u),
    locationId: form.locationId,
    model: form.model,
    name: form.name,
    price: form.price,
    quantity: form.quantity,
    status: form.status,
  };
}

function beforeImageUpload(file: File) {
  const allowed = ['image/gif', 'image/jpeg', 'image/png', 'image/webp'];
  if (!allowed.includes(file.type)) {
    ElMessage.warning('仅支持 jpg/png/gif/webp 格式图片');
    return false;
  }
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.warning('单张图片大小不能超过 5MB');
    return false;
  }
  return true;
}

async function customImageUpload(options: UploadRequestOptions) {
  // 返回值会成为该文件的 response,buildPayload 据此取 url
  return await uploadAssetImageApi(options.file);
}

function onImageExceed() {
  ElMessage.warning('最多上传 5 张照片');
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
  borrowForm.returnDate = '';
  borrowForm.reason = '';
  borrowDialogVisible.value = true;
}

function openTransferDialog(row: AssetItem) {
  currentAssetForAction.value = row;
  transferForm.transfereeId = undefined;
  transferForm.reason = '';
  transferDialogVisible.value = true;
}

async function submitBorrow() {
  if (!currentAssetForAction.value) return;
  if (!borrowForm.returnDate) {
    ElMessage.warning('请选择归还日期');
    return;
  }
  if (!borrowForm.reason || borrowForm.reason.length < 10 || borrowForm.reason.length > 200) {
    ElMessage.warning('借用原因需要 10-200 字');
    return;
  }

  try {
    saving.value = true;
    await startApprovalApi({
      bizType: 'borrow',
      assetId: currentAssetForAction.value.id,
      returnDate: borrowForm.returnDate as string,
      reason: borrowForm.reason,
    });
    ElMessage.success('借用申请已提交');
    borrowDialogVisible.value = false;
    window.location.href = '/#/approval/mine';
  } finally {
    saving.value = false;
  }
}

async function submitTransfer() {
  if (!currentAssetForAction.value) return;
  if (!transferForm.transfereeId) {
    ElMessage.warning('请选择受让人');
    return;
  }
  if (!transferForm.reason || transferForm.reason.length < 10 || transferForm.reason.length > 200) {
    ElMessage.warning('转让原因需要 10-200 字');
    return;
  }

  try {
    saving.value = true;
    await startApprovalApi({
      bizType: 'transfer',
      assetId: currentAssetForAction.value.id,
      transfereeId: transferForm.transfereeId,
      reason: transferForm.reason,
    });
    ElMessage.success('转让申请已提交');
    transferDialogVisible.value = false;
    window.location.href = '/#/approval/mine';
  } finally {
    saving.value = false;
  }
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
                  <ElTableColumn fixed="right" label="操作" width="200">
                    <template #default="{ row }">
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
                    <ElButton @click="ElMessage.info('打印标签功能待接入打印服务')">
                      批量打印标签
                    </ElButton>
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
                  <ElTableColumn fixed="right" label="操作" width="200">
                    <template #default="{ row }">
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

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑资产' : '新增资产'"
        width="560px"
      >
        <ElForm label-width="88px">
          <ElFormItem label="资产名称">
            <ElInput v-model="form.name" />
          </ElFormItem>
          <ElFormItem label="资产分类">
            <ElSelect v-model="form.categoryId" filterable placeholder="选择末级分类" style="width: 100%">
              <ElOption
                v-for="item in categoryOptions"
                :key="item.id"
                :label="item.label"
                :value="item.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="编号预览">
            <ElTag>{{ assetNoPreview }}</ElTag>
          </ElFormItem>
          <ElFormItem label="归属部门">
            <ElSelect v-model="form.departmentId" clearable filterable placeholder="选择部门" style="width: 100%">
              <ElOption
                v-for="item in departmentOptions"
                :key="item.id"
                :label="item.label"
                :value="item.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="存放位置">
            <ElSelect v-model="form.locationId" clearable filterable placeholder="选择位置" style="width: 100%">
              <ElOption
                v-for="item in locationOptions"
                :key="item.id"
                :label="item.label"
                :value="item.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="型号品牌">
            <div class="grid w-full grid-cols-2 gap-2">
              <ElInput v-model="form.model" placeholder="型号" />
              <ElInput v-model="form.brand" placeholder="品牌" />
            </div>
          </ElFormItem>
          <ElFormItem label="价格数量">
            <div class="grid w-full grid-cols-2 gap-2">
              <ElInputNumber v-model="form.price" :min="0" :precision="2" class="w-full" />
              <ElInputNumber v-model="form.quantity" :min="1" class="w-full" />
            </div>
          </ElFormItem>
          <ElFormItem label="资产照片">
            <ElUpload
              v-model:file-list="imageFileList"
              :before-upload="beforeImageUpload"
              :http-request="customImageUpload"
              :limit="5"
              :on-exceed="onImageExceed"
              accept="image/png,image/jpeg,image/gif,image/webp"
              list-type="picture-card"
            >
              <span class="text-2xl">+</span>
            </ElUpload>
          </ElFormItem>
          <ElFormItem v-if="editingId" label="状态">
            <ElSelect v-model="form.status" style="width: 100%">
              <ElOption
                v-for="item in statusOptions"
                :key="item.value"
                :label="item.label"
                :value="item.value"
              />
            </ElSelect>
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">保存</ElButton>
        </template>
      </ElDialog>

      <ElDialog v-model="importVisible" title="批量导入资产" width="760px">
        <div class="space-y-3">
          <div class="flex flex-wrap items-center gap-2">
            <ElButton @click="downloadTemplate">下载模板</ElButton>
            <input accept=".xlsx" type="file" @change="onFileChange" />
            <ElButton :loading="importing" @click="validateImport">预校验</ElButton>
            <ElButton
              :disabled="!importPreview.some((row) => row.isValid)"
              :loading="importing"
              type="primary"
              @click="confirmImport"
            >
              确认导入
            </ElButton>
          </div>
          <ElTable :data="importPreview" border max-height="360">
            <ElTableColumn label="行号" prop="row" width="80" />
            <ElTableColumn label="名称" prop="name" />
            <ElTableColumn label="分类编码" prop="categoryCode" />
            <ElTableColumn label="单价" prop="price" width="100" />
            <ElTableColumn label="状态" width="90">
              <template #default="{ row }">
                <ElTag :type="row.isValid ? 'success' : 'danger'">
                  {{ row.isValid ? '有效' : '无效' }}
                </ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="错误" min-width="180" prop="error" />
          </ElTable>
        </div>
      </ElDialog>

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

      <ElDialog v-model="borrowDialogVisible" title="资产借用申请" width="560px">
        <ElForm v-if="currentAssetForAction" label-width="88px">
          <ElFormItem label="资产编号">
            <ElInput v-model="currentAssetForAction.assetNo" disabled />
          </ElFormItem>
          <ElFormItem label="资产名称">
            <ElInput v-model="currentAssetForAction.name" disabled />
          </ElFormItem>
          <ElFormItem label="归还日期">
            <ElDatePicker
              v-model="borrowForm.returnDate"
              clearable
              format="YYYY-MM-DD"
              placeholder="选择归还日期"
              style="width: 100%"
              type="date"
            />
          </ElFormItem>
          <ElFormItem label="借用原因">
            <ElInput
              v-model="borrowForm.reason"
              :maxlength="200"
              clearable
              placeholder="10-200 字"
              :rows="3"
              show-word-limit
              type="textarea"
            />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="borrowDialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="submitBorrow">提交</ElButton>
        </template>
      </ElDialog>

      <ElDialog v-model="transferDialogVisible" title="资产转让申请" width="560px">
        <ElForm v-if="currentAssetForAction" label-width="88px">
          <ElFormItem label="资产编号">
            <ElInput v-model="currentAssetForAction.assetNo" disabled />
          </ElFormItem>
          <ElFormItem label="资产名称">
            <ElInput v-model="currentAssetForAction.name" disabled />
          </ElFormItem>
          <ElFormItem label="受让人">
            <ElSelect v-model="transferForm.transfereeId" filterable placeholder="选择受让人" style="width: 100%">
              <ElOption
                v-for="item in users"
                :key="item.id"
                :label="`${item.name}(${item.employeeNo})`"
                :value="item.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="转让原因">
            <ElInput
              v-model="transferForm.reason"
              :maxlength="200"
              clearable
              placeholder="10-200 字"
              :rows="3"
              show-word-limit
              type="textarea"
            />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="transferDialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="submitTransfer">提交</ElButton>
        </template>
      </ElDialog>
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
