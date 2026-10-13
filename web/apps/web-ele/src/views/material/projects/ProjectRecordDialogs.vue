<script lang="ts" setup>
import { useProjectPageContext } from './project-page-context';
import ProjectFormDialog from './ProjectFormDialog.vue';
import ProjectImportDialog from './ProjectImportDialog.vue';
import ProjectOptionDialog from './ProjectOptionDialog.vue';
import ProjectProgressDialog from './ProjectProgressDialog.vue';

const {
  activeOptionKind,
  dialogVisible,
  displayedOptions,
  editingId,
  formModel,
  importDialogVisible,
  loadData,
  openOptionCreate,
  openOptionEdit,
  optionDialogVisible,
  optionEditingId,
  optionFormModel,
  optionSaving,
  progressDialogVisible,
  progressFormModel,
  progressOptions,
  progressSaving,
  projectActionAccess,
  projectTypeOptions,
  removeOption,
  save,
  saveOption,
  saveProgress,
  saving,
  searchUsers,
  userOptionsLoading,
  users,
} = useProjectPageContext();
</script>

<template>
  <ProjectFormDialog
    v-model:form="formModel"
    v-model:visible="dialogVisible"
    :editing="editingId !== null"
    :progress-options="progressOptions"
    :project-type-options="projectTypeOptions"
    :saving="saving"
    :search-users="searchUsers"
    :user-options-loading="userOptionsLoading"
    :users="users"
    @save="save"
  />

  <ProjectImportDialog
    v-model:visible="importDialogVisible"
    @imported="loadData"
  />

  <ProjectProgressDialog
    v-model:form="progressFormModel"
    v-model:visible="progressDialogVisible"
    :progress-options="progressOptions"
    :saving="progressSaving"
    @save="saveProgress"
  />

  <ProjectOptionDialog
    v-model:active-kind="activeOptionKind"
    v-model:form="optionFormModel"
    v-model:visible="optionDialogVisible"
    :can-manage="projectActionAccess.canOption"
    :displayed-options="displayedOptions"
    :editing-id="optionEditingId"
    :saving="optionSaving"
    @edit="openOptionEdit"
    @remove="removeOption"
    @reset="openOptionCreate"
    @save="saveOption"
  />
</template>
