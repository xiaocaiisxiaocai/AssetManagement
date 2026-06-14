<script lang="ts" setup>
import type { AssetItem, AssetPayload, AssetQuery, AssetStatus, ImportPreviewRow } from '#/api/asset';
import type { CategoryNode, DepartmentNode, LocationNode } from '#/api/base-data';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  confirmAssetImportApi,
  createAssetApi,
  deleteAssetApi,
  downloadAssetTemplateApi,
  exportAssetsApi,
  getAssetListApi,
  updateAssetApi,
  validateAssetImportApi,
} from '#/api/asset';
import {
  getCategoryTreeApi,
  getDepartmentTreeApi,
  getLocationTreeApi,
} from '#/api/base-data';

import {
  ElButton,
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
  ElTable,
  ElTableColumn,
  ElTabs,
  ElTag,
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

const activeView = ref<'hierarchy' | 'list'>('list');
const loading = ref(false);
const saving = ref(false);
const importing = ref(false);
const dialogVisible = ref(false);
const importVisible = ref(false);
const editingId = ref<null | number>(null);
const selectedCategoryId = ref<null | number>(null);
const selectedFile = ref<File | null>(null);
const importPreview = ref<ImportPreviewRow[]>([]);
const assets = ref<AssetItem[]>([]);
const total = ref(0);
const categories = ref<CategoryNode[]>([]);
const departments = ref<DepartmentNode[]>([]);
const locations = ref<LocationNode[]>([]);

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

const categoryOptions = computed(() => flattenCategories(categories.value));
const departmentOptions = computed(() => flattenDepartments(departments.value));
const locationOptions = computed(() => flattenLocations(locations.value));
const selectedCategory = computed(() =>
  categoryOptions.value.find((item) => item.id === form.categoryId),
);
const assetNoPreview = computed(() =>
  selectedCategory.value?.code ? `${selectedCategory.value.code}-自动流水` : '选择分类后生成',
);

async function loadDictionaries() {
  const [categoryTree, departmentTree, locationTree] = await Promise.all([
    getCategoryTreeApi(),
    getDepartmentTreeApi(),
    getLocationTreeApi(),
  ]);
  categories.value = categoryTree;
  departments.value = departmentTree;
  locations.value = locationTree;
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
    await loadData();
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
  await loadData();
}

function selectCategory(categoryId: number) {
  selectedCategoryId.value = categoryId;
  query.categoryId = categoryId;
  query.page = 1;
  void loadData();
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
    await loadData();
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
    locationId: form.locationId,
    model: form.model,
    name: form.name,
    price: form.price,
    quantity: form.quantity,
    status: form.status,
  };
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

onMounted(async () => {
  await loadDictionaries();
  await loadData();
});
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">资产列表</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            管理资产档案、自动编号、导入导出和分类层级视图。
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <ElButton @click="downloadTemplate">下载模板</ElButton>
          <ElButton @click="openImport">批量导入</ElButton>
          <ElButton @click="exportAssets">导出</ElButton>
          <ElButton type="primary" @click="openCreate()">新增资产</ElButton>
        </div>
      </div>

      <div class="rounded border bg-card p-4">
        <ElForm inline>
          <ElFormItem label="资产编号">
            <ElInput v-model="query.assetNo" clearable placeholder="输入编号" />
          </ElFormItem>
          <ElFormItem label="资产名称">
            <ElInput v-model="query.name" clearable placeholder="输入名称" />
          </ElFormItem>
          <ElFormItem label="分类">
            <ElSelect
              v-model="query.categoryId"
              clearable
              filterable
              placeholder="全部分类"
              style="width: 220px"
            >
              <ElOption
                v-for="item in categoryOptions"
                :key="item.id"
                :label="item.label"
                :value="item.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="部门">
            <ElSelect
              v-model="query.departmentId"
              clearable
              filterable
              placeholder="全部部门"
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
          <ElFormItem label="状态">
            <ElSelect
              v-model="query.status"
              clearable
              placeholder="全部状态"
              style="width: 130px"
            >
              <ElOption
                v-for="item in statusOptions"
                :key="item.value"
                :label="item.label"
                :value="item.value"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem>
            <ElButton type="primary" @click="search">查询</ElButton>
            <ElButton @click="resetQuery">重置</ElButton>
          </ElFormItem>
        </ElForm>
      </div>

      <ElTabs v-model="activeView">
        <ElTabPane label="列表视图" name="list">
          <ElTable v-loading="loading" :data="assets" border>
            <ElTableColumn label="资产编号" min-width="170" prop="assetNo" />
            <ElTableColumn label="名称" min-width="140" prop="name" />
            <ElTableColumn label="分类" min-width="170">
              <template #default="{ row }">
                <div>{{ row.categoryName }}</div>
                <ElTag size="small">{{ row.categoryCode }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn label="归属部门" min-width="120" prop="departmentName" />
            <ElTableColumn label="位置" min-width="120" prop="locationName" />
            <ElTableColumn label="型号" min-width="120" prop="model" />
            <ElTableColumn label="单价" min-width="100" prop="price" />
            <ElTableColumn label="状态" width="90">
              <template #default="{ row }">
                <ElTag :type="statusMeta(row.status).tag">
                  {{ statusMeta(row.status).label }}
                </ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn fixed="right" label="操作" width="150">
              <template #default="{ row }">
                <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
                <ElButton link type="danger" @click="remove(row)">删除</ElButton>
              </template>
            </ElTableColumn>
          </ElTable>
        </ElTabPane>

        <ElTabPane label="层级视图" name="hierarchy">
          <div class="grid gap-4 lg:grid-cols-[280px_1fr]">
            <div class="rounded border bg-card p-3">
              <div class="mb-3 flex items-center justify-between">
                <span class="font-medium">分类树</span>
                <ElButton size="small" @click="openCreate(selectedCategoryId ?? undefined)">
                  末级新增
                </ElButton>
              </div>
              <div class="max-h-[520px] space-y-1 overflow-auto">
                <button
                  v-for="item in categoryOptions"
                  :key="item.id"
                  class="block w-full rounded px-2 py-1 text-left text-sm hover:bg-muted"
                  :class="{ 'bg-primary text-primary-foreground': selectedCategoryId === item.id }"
                  @click="selectCategory(item.id)"
                >
                  {{ item.label }}
                </button>
              </div>
            </div>
            <ElTable v-loading="loading" :data="assets" border>
              <ElTableColumn label="资产编号" min-width="180" prop="assetNo" />
              <ElTableColumn label="资产名称" min-width="160" prop="name" />
              <ElTableColumn label="部门" min-width="120" prop="departmentName" />
              <ElTableColumn label="位置" min-width="120" prop="locationName" />
              <ElTableColumn label="状态" width="90">
                <template #default="{ row }">
                  <ElTag :type="statusMeta(row.status).tag">
                    {{ statusMeta(row.status).label }}
                  </ElTag>
                </template>
              </ElTableColumn>
              <ElTableColumn fixed="right" label="操作" width="150">
                <template #default="{ row }">
                  <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
                  <ElButton link type="danger" @click="remove(row)">删除</ElButton>
                </template>
              </ElTableColumn>
            </ElTable>
          </div>
        </ElTabPane>
      </ElTabs>

      <div class="flex justify-end">
        <ElPagination
          v-model:current-page="query.page"
          v-model:page-size="query.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="total"
          background
          layout="total, sizes, prev, pager, next"
          @current-change="loadData"
          @size-change="search"
        />
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
    </div>
  </re-page>
</template>
