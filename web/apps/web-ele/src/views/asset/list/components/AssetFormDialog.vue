<script lang="ts" setup>
import type { AssetFormDialogProps } from './use-asset-form-dialog';

import { ElButton, ElDialog, ElForm } from 'element-plus';

import AssetFormFields from './AssetFormFields.vue';
import { useAssetFormDialog } from './use-asset-form-dialog';

const props = defineProps<AssetFormDialogProps>();
const emit = defineEmits<{ saved: [] }>();
const visible = defineModel<boolean>('visible', { default: false });
const {
  assetNoPreview,
  beforeImageUpload,
  canUploadImages,
  customImageUpload,
  debouncedSave,
  form,
  imageFileList,
  isEdit,
  onImageExceed,
  onImageRemove,
  saving,
  searchCustodians,
  selectableCategoryOptions,
  selectableConditionOptions,
  selectableDepartmentOptions,
  statusOptions,
  uploading,
} = useAssetFormDialog({
  onSaved: () => emit('saved'),
  props,
  visible,
});
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="isEdit ? '编辑资产' : '新增资产'"
    align-center
    class="asset-form-dialog"
    width="840px"
  >
    <ElForm label-width="88px">
      <AssetFormFields
        v-model:form="form"
        v-model:image-file-list="imageFileList"
        :asset-no-preview="assetNoPreview"
        :before-image-upload="beforeImageUpload"
        :can-upload-images="canUploadImages"
        :category-options="selectableCategoryOptions"
        :custom-image-upload="customImageUpload"
        :is-edit="isEdit"
        :on-image-exceed="onImageExceed"
        :on-image-remove="onImageRemove"
        :search-custodians="searchCustodians"
        :selectable-condition-options="selectableConditionOptions"
        :selectable-department-options="selectableDepartmentOptions"
        :status-options="statusOptions"
        :user-options-loading="userOptionsLoading"
        :users="users"
      />
    </ElForm>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton
        :loading="saving || uploading"
        type="primary"
        @click="debouncedSave"
      >
        保存
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./AssetFormDialog.css"></style>
