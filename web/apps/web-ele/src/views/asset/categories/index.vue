<script lang="ts" setup>
import type { CategoryNode, CategoryPayload } from '#/api/base-data';

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
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  createCategoryApi,
  deleteCategoryApi,
  getCategoryTreeApi,
  getRuntimeSettingsApi,
  purgeCategoryApi,
  restoreCategoryApi,
  updateCategoryApi,
} from '#/api/base-data';
import { buildCategoryActionAccess } from '#/views/permissions/action-access';

import {
  categoryCodeRuleHint,
  type CategoryCodeRules,
  defaultCategoryCodeRules,
  validateCategoryCodeSeg,
} from './category-code-rules';

defineOptions({ name: 'AssetCategories' });

const { hasAccessByCodes } = useAccess();

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const editingId = ref<null | number>(null);
const parentCode = ref('');
const categories = ref<CategoryNode[]>([]);
const categoryCodeRules = ref<CategoryCodeRules>(defaultCategoryCodeRules);
const MAX_CATEGORY_LEVEL = 3;
const categoryActionAccess = computed(() =>
  buildCategoryActionAccess(hasAccessByCodes),
);
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
const currentCategoryLevel = computed(() => {
  if (!form.parentId) return 1;
  const parent = findNode(categories.value, form.parentId);
  return parent ? categoryLevel(parent) + 1 : 1;
});
const codeRuleHint = computed(() =>
  categoryCodeRuleHint(currentCategoryLevel.value, categoryCodeRules.value),
);

async function loadData() {
  loading.value = true;
  try {
    const [tree, runtimeSettings] = await Promise.all([
      getCategoryTreeApi('all'),
      getRuntimeSettingsApi(),
    ]);
    categories.value = tree;
    categoryCodeRules.value =
      runtimeSettings.categoryCodeRules ?? defaultCategoryCodeRules;
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
  if (saving.value) return;

  const validationMessage = validateCategoryCodeSeg(
    form.codeSeg,
    currentCategoryLevel.value,
    categoryCodeRules.value,
  );
  if (validationMessage) {
    ElMessage.warning(validationMessage);
    return;
  }
  const payload: CategoryPayload = {
    codeSeg: form.codeSeg.trim(),
    parentId: form.parentId,
    remark: isRootCategory.value ? null : form.remark?.trim() || null,
  };
  saving.value = true;
  try {
    await (editingId.value
      ? updateCategoryApi(editingId.value, payload)
      : createCategoryApi(payload));
    ElMessage.success('保存成功');
    dialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function remove(row: CategoryNode) {
  try {
    await ElMessageBox.confirm(
      `确认删除分类「${row.code}」？子分类会一并删除，删除后仍显示在列表中，可由管理员彻底删除。`,
      '删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  await deleteCategoryApi(row.id);
  ElMessage.success('已删除');
  await loadData();
}

async function purge(row: CategoryNode) {
  try {
    await ElMessageBox.confirm(
      `彻底删除分类「${row.code}」后不可恢复，确认继续？`,
      '彻底删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  await purgeCategoryApi(row.id);
  ElMessage.success('已彻底删除');
  await loadData();
}

async function restore(row: CategoryNode) {
  try {
    await ElMessageBox.confirm(
      `确认撤销删除分类「${row.code}」？将连同其子分类一并恢复。`,
      '撤销删除确认',
      { type: 'warning' },
    );
  } catch {
    return;
  }
  await restoreCategoryApi(row.id);
  ElMessage.success('已恢复');
  await loadData();
}

function tableRowClassName({ row }: { row: CategoryNode }) {
  return row.isDeleted ? 'category-row-deleted' : '';
}

function findNode(
  nodes: CategoryNode[],
  id?: null | number,
): CategoryNode | null {
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
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2 class="page-title">资产分类编码树</h2>
        </div>
        <div class="flex gap-2">
          <ElButton
            v-if="categoryActionAccess.canCreate"
            type="primary"
            @click="openCreate()"
          >
            新增顶级分类
          </ElButton>
        </div>
      </div>

      <div class="table-panel">
        <ElTable
          :data="categories"
          :row-class-name="tableRowClassName"
          border
          default-expand-all
          height="100%"
          row-key="id"
          v-loading="loading"
        >
          <ElTableColumn label="编码段" min-width="140" prop="codeSeg" />
          <ElTableColumn label="完整编码" min-width="200">
            <template #default="{ row }">
              <ElTag size="default">{{ row.code }}</ElTag>
              <ElTag
                v-if="row.isDeleted"
                class="ml-1"
                size="small"
                type="danger"
              >
                已删除
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn
            class-name="hide-on-mobile"
            label="备注"
            min-width="260"
          >
            <template #default="{ row }">
              <div class="category-remark">
                {{ row.parentId ? row.remark || '-' : '-' }}
              </div>
            </template>
          </ElTableColumn>
          <ElTableColumn align="center" fixed="right" label="操作" width="260">
            <template #default="{ row }">
              <template v-if="!row.isDeleted">
                <ElButton
                  v-if="categoryActionAccess.canCreate && canCreateChild(row)"
                  link
                  size="small"
                  type="primary"
                  @click="openCreate(row)"
                >
                  新增下级
                </ElButton>
                <ElButton
                  v-if="categoryActionAccess.canEdit"
                  link
                  size="small"
                  type="primary"
                  @click="openEdit(row)"
                >
                  编辑
                </ElButton>
                <ElButton
                  v-if="categoryActionAccess.canDelete"
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
                  v-if="categoryActionAccess.canRestore"
                  link
                  size="small"
                  type="success"
                  @click="restore(row)"
                >
                  撤销删除
                </ElButton>
                <ElButton
                  v-if="categoryActionAccess.canPurge"
                  link
                  size="small"
                  type="danger"
                  @click="purge(row)"
                >
                  彻底删除
                </ElButton>
                <span
                  v-if="
                    !categoryActionAccess.canRestore &&
                    !categoryActionAccess.canPurge
                  "
                  class="asset-no-permission"
                >
                  无操作权限
                </span>
              </template>
            </template>
          </ElTableColumn>
        </ElTable>
      </div>

      <ElDialog
        v-model="dialogVisible"
        :title="editingId ? '编辑分类' : '新增分类'"
        width="500px"
      >
        <ElForm label-width="100px" @submit.prevent="save">
          <ElFormItem label="上级分类">
            <ElTag size="default">{{ parentDisplay }}</ElTag>
          </ElFormItem>
          <ElFormItem label="编码段" required>
            <ElInput v-model="form.codeSeg" placeholder="请输入编码段" />
            <div class="form-tip">{{ codeRuleHint }}</div>
          </ElFormItem>
          <ElFormItem v-if="!isRootCategory" label="备注">
            <ElInput
              v-model="form.remark"
              :rows="3"
              placeholder="请输入备注信息"
              type="textarea"
            />
          </ElFormItem>
          <ElFormItem label="完整编码">
            <ElTag size="default" type="info">
              {{ previewCode || '待输入编码段' }}
            </ElTag>
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="save">
            保存
          </ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>

<style scoped>
.category-remark {
  word-break: break-word;
  white-space: pre-wrap;
}

.table-panel :deep(.category-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: var(--el-fill-color-light);
}

.asset-no-permission {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.form-tip {
  margin-top: 6px;
  font-size: 12px;
  line-height: 18px;
  color: var(--el-text-color-secondary);
}
</style>
