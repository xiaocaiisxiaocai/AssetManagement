<script lang="ts" setup>
import type { CategoryNode, CategoryPayload } from '#/api/base-data';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  createCategoryApi,
  deleteCategoryApi,
  getCategoryTreeApi,
  updateCategoryApi,
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
  ElTag,
} from 'element-plus';

defineOptions({ name: 'AssetCategories' });

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const parentCode = ref('');
const categories = ref<CategoryNode[]>([]);
const MAX_CATEGORY_LEVEL = 3;
const form = reactive<CategoryPayload>({
  codeSeg: '',
  parentId: null,
  remark: '',
});

const previewCode = computed(() =>
  parentCode.value ? `${parentCode.value}-${form.codeSeg}` : form.codeSeg,
);
const isRootCategory = computed(() => !form.parentId);
const parentDisplay = computed(() => parentCode.value || '顶级分类');

async function loadData() {
  loading.value = true;
  try {
    categories.value = await getCategoryTreeApi();
  } finally {
    loading.value = false;
  }
}

function openCreate(parent?: CategoryNode) {
  if (parent && !canCreateChild(parent)) {
    ElMessage.warning('资产分类最多维护三级');
    return;
  }
  editingId.value = null;
  parentCode.value = parent?.code ?? '';
  Object.assign(form, {
    codeSeg: '',
    parentId: parent?.id ?? null,
    remark: '',
  });
  dialogVisible.value = true;
}

function openEdit(row: CategoryNode) {
  editingId.value = row.id;
  const parent = findNode(categories.value, row.parentId);
  parentCode.value = parent?.code ?? '';
  Object.assign(form, {
    codeSeg: row.codeSeg,
    parentId: row.parentId ?? null,
    remark: row.remark ?? '',
  });
  dialogVisible.value = true;
}

async function save() {
  if (!form.codeSeg.trim()) {
    ElMessage.warning('请填写编码段');
    return;
  }
  const payload: CategoryPayload = {
    codeSeg: form.codeSeg.trim(),
    parentId: form.parentId,
    remark: isRootCategory.value ? null : form.remark?.trim() || null,
  };
  saving.value = true;
  try {
    if (editingId.value) {
      await updateCategoryApi(editingId.value, payload);
    } else {
      await createCategoryApi(payload);
    }
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function remove(row: CategoryNode) {
  await ElMessageBox.confirm(
    `确认删除分类「${row.code}」？子分类会一并删除。`,
    '删除确认',
    { type: 'warning' },
  );
  await deleteCategoryApi(row.id);
  ElMessage.success('删除成功');
  await loadData();
}

function findNode(nodes: CategoryNode[], id?: null | number): CategoryNode | null {
  if (!id) return null;
  for (const node of nodes) {
    if (node.id === id) return node;
    const found = findNode(node.children, id);
    if (found) return found;
  }
  return null;
}

function categoryLevel(row: CategoryNode) {
  let level = 1;
  let parentId = row.parentId;
  while (parentId) {
    const parent = findNode(categories.value, parentId);
    if (!parent) break;
    level++;
    parentId = parent.parentId;
  }
  return level;
}

function canCreateChild(row: CategoryNode) {
  return categoryLevel(row) < MAX_CATEGORY_LEVEL;
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">资产分类编码树</h2>
        </div>
        <ElButton type="primary" @click="openCreate()">新增顶级分类</ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="categories"
        row-key="id"
        border
        default-expand-all
      >
        <ElTableColumn label="编码段" min-width="120" prop="codeSeg" />
        <ElTableColumn label="完整编码" min-width="220">
          <template #default="{ row }">
            <ElTag>{{ row.code }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="备注" min-width="220">
          <template #default="{ row }">
            <div class="category-remark">
              {{ row.parentId ? row.remark || '-' : '-' }}
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn fixed="right" label="操作" width="240">
          <template #default="{ row }">
            <ElButton
              v-if="canCreateChild(row)"
              link
              type="primary"
              @click="openCreate(row)"
            >
              新增下级
            </ElButton>
            <ElButton link type="primary" @click="openEdit(row)">编辑</ElButton>
            <ElButton link type="danger" @click="remove(row)">删除</ElButton>
          </template>
        </ElTableColumn>
      </ElTable>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑分类' : '新增分类'"
        width="460px"
      >
        <ElForm label-width="88px">
          <ElFormItem label="上级分类">
            <ElTag>{{ parentDisplay }}</ElTag>
          </ElFormItem>
          <ElFormItem label="编码段">
            <ElInput v-model="form.codeSeg" />
          </ElFormItem>
          <ElFormItem v-if="!isRootCategory" label="备注">
            <ElInput v-model="form.remark" :rows="3" type="textarea" />
          </ElFormItem>
          <ElFormItem label="完整编码">
            <ElTag>{{ previewCode || '待输入' }}</ElTag>
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
.category-remark {
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
