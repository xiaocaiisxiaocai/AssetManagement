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
const form = reactive<CategoryPayload>({
  codeSeg: '',
  name: '',
  parentId: null,
});

const previewCode = computed(() =>
  parentCode.value ? `${parentCode.value}-${form.codeSeg}` : form.codeSeg,
);

async function loadData() {
  loading.value = true;
  try {
    categories.value = await getCategoryTreeApi();
  } finally {
    loading.value = false;
  }
}

function openCreate(parent?: CategoryNode) {
  editingId.value = null;
  parentCode.value = parent?.code ?? '';
  Object.assign(form, {
    codeSeg: '',
    name: '',
    parentId: parent?.id ?? null,
  });
  dialogVisible.value = true;
}

function openEdit(row: CategoryNode) {
  editingId.value = row.id;
  const parent = findNode(categories.value, row.parentId);
  parentCode.value = parent?.code ?? '';
  Object.assign(form, {
    codeSeg: row.codeSeg,
    name: row.name,
    parentId: row.parentId ?? null,
  });
  dialogVisible.value = true;
}

async function save() {
  if (!form.name.trim() || !form.codeSeg.trim()) {
    ElMessage.warning('请填写分类名称和编码段');
    return;
  }
  saving.value = true;
  try {
    if (editingId.value) {
      await updateCategoryApi(editingId.value, form);
    } else {
      await createCategoryApi(form);
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
    `确认删除分类「${row.name}」？子分类会一并删除。`,
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

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">资产分类编码树</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            完整编码由父级编码和本层编码段逐级拼接。
          </p>
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
        <ElTableColumn label="分类名称" min-width="180" prop="name" />
        <ElTableColumn label="编码段" min-width="120" prop="codeSeg" />
        <ElTableColumn label="完整编码" min-width="220">
          <template #default="{ row }">
            <ElTag>{{ row.code }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn fixed="right" label="操作" width="240">
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
        :title="editingId ? '编辑分类' : '新增分类'"
        width="460px"
      >
        <ElForm label-width="88px">
          <ElFormItem label="上级 ID">
            <ElInput v-model.number="form.parentId" clearable placeholder="留空为顶级" />
          </ElFormItem>
          <ElFormItem label="分类名称">
            <ElInput v-model="form.name" />
          </ElFormItem>
          <ElFormItem label="编码段">
            <ElInput v-model="form.codeSeg" />
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
