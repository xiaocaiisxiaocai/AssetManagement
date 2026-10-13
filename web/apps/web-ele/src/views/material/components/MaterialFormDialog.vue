<script lang="ts" setup>
import type { MaterialFormDialogProps } from './use-material-form-dialog';

import { ElButton, ElDialog, ElForm } from 'element-plus';

import MaterialFormFields from './MaterialFormFields.vue';
import { useMaterialFormDialog } from './use-material-form-dialog';

const props = defineProps<MaterialFormDialogProps>();
const emit = defineEmits<{ saved: [] }>();
const visible = defineModel<boolean>('visible', { default: false });
const {
  beforeImageUpload,
  canUploadImages,
  customImageUpload,
  debouncedSave,
  form,
  imageFileList,
  isCustodianDepartmentRequired,
  isEdit,
  onImageExceed,
  onImageRemove,
  saving,
  searchCustodians,
  selectableDepartmentOptions,
  syncCustodianDepartment,
  uploading,
} = useMaterialFormDialog({
  onSaved: () => emit('saved'),
  props,
  visible,
});
</script>

<template>
  <ElDialog
    v-model="visible"
    :title="isEdit ? '编辑测试料件' : '新增测试料件'"
    align-center
    class="material-form-dialog"
    width="840px"
  >
    <ElForm label-width="96px">
      <MaterialFormFields
        v-model:form="form"
        v-model:image-file-list="imageFileList"
        :before-image-upload="beforeImageUpload"
        :can-upload-images="canUploadImages"
        :custom-image-upload="customImageUpload"
        :is-custodian-department-required="isCustodianDepartmentRequired"
        :is-edit="isEdit"
        :on-image-exceed="onImageExceed"
        :on-image-remove="onImageRemove"
        :project-locked="props.projectLocked"
        :projects="projects"
        :search-custodians="searchCustodians"
        :selectable-department-options="selectableDepartmentOptions"
        :sync-custodian-department="syncCustodianDepartment"
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

<style scoped src="./MaterialFormDialog.css"></style>
