import type { CategoryNode, CategoryPayload } from '#/api/base-data';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  createCategoryApi,
  deleteCategoryApi,
  getCategoryTreeApi,
  getRuntimeSettingsApi,
  purgeCategoryApi,
  restoreCategoryApi,
  updateCategoryApi,
} from '#/api/base-data';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import { buildCategoryActionAccess } from '#/views/permissions/action-access';

import {
  categoryCodeRuleHint,
  type CategoryCodeRules,
  defaultCategoryCodeRules,
  validateCategoryCodeSeg,
} from './category-code-rules';

export function useCategoryPage() {
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
  const formModel = reactiveObjectModel(form);

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

  return {
    loading,
    saving,
    dialogVisible,
    editingId,
    parentCode,
    categories,
    categoryCodeRules,
    MAX_CATEGORY_LEVEL,
    categoryActionAccess,
    form,
    formModel,
    previewCode,
    isRootCategory,
    parentDisplay,
    currentCategoryLevel,
    codeRuleHint,
    loadData,
    openCreate,
    openEdit,
    save,
    remove,
    purge,
    restore,
    findNode,
    categoryLevel,
    canCreateChild,
  };
}
