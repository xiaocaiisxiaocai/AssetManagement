<script lang="ts" setup>
import { ElButton } from 'element-plus';

import CategoryFormDialog from './CategoryFormDialog.vue';
import CategoryTreeTable from './CategoryTreeTable.vue';
import { useCategoryPage } from './use-category-page';

defineOptions({ name: 'AssetCategories' });

const {
  loading,
  saving,
  dialogVisible,
  editingId,
  categories,
  categoryActionAccess,
  formModel,
  previewCode,
  isRootCategory,
  parentDisplay,
  codeRuleHint,
  openCreate,
  openEdit,
  save,
  remove,
  purge,
  restore,
  canCreateChild,
} = useCategoryPage();
</script>

<template>
  <re-page>
    <div class="page-container page-fit">
      <div class="page-header">
        <div>
          <h2 class="page-title">资产分类编码树</h2>
        </div>
        <div class="page-actions">
          <span class="column-tools-host" data-column-tools="admin-categories"></span>
          <ElButton
            v-if="categoryActionAccess.canCreate"
            type="primary"
            @click="openCreate()"
          >
            新增顶级分类
          </ElButton>
        </div>
      </div>

      <CategoryTreeTable
        :access="categoryActionAccess"
        :can-create-child="canCreateChild"
        :categories="categories"
        :loading="loading"
        @create="openCreate"
        @edit="openEdit"
        @purge="purge"
        @remove="remove"
        @restore="restore"
      />

      <CategoryFormDialog
        v-model:form="formModel"
        v-model:visible="dialogVisible"
        :code-rule-hint="codeRuleHint"
        :editing="editingId !== null"
        :is-root-category="isRootCategory"
        :parent-display="parentDisplay"
        :preview-code="previewCode"
        :saving="saving"
        @save="save"
      />
    </div>
  </re-page>
</template>
