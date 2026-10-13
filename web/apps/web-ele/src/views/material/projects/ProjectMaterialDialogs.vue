<script lang="ts" setup>
import WorkflowNodeSelectDialog from '#/components/workflow/WorkflowNodeSelectDialog.vue';

import MaterialDetailDialog from '../components/MaterialDetailDialog.vue';
import MaterialFormDialog from '../components/MaterialFormDialog.vue';
import TransferDialog from '../components/TransferDialog.vue';
import { useProjectPageContext } from './project-page-context';

const {
  activeDepartmentOptions,
  afterMaterialChanged,
  currentProject,
  currentProjectList,
  editingMaterial,
  materialDetail,
  materialDetailLoading,
  materialDetailVisible,
  materialFormVisible,
  searchUsers,
  transferMaterial,
  transferVisible,
  userOptionsLoading,
  users,
  workflowNodeSelector,
} = useProjectPageContext();
</script>

<template>
  <MaterialFormDialog
    v-model:visible="materialFormVisible"
    :default-project-id="currentProject?.id"
    :department-options="activeDepartmentOptions"
    :material="editingMaterial"
    :project-locked="true"
    :projects="currentProjectList"
    :search-users="searchUsers"
    :user-options-loading="userOptionsLoading"
    :users="users"
    @saved="afterMaterialChanged"
  />

  <MaterialDetailDialog
    v-model:visible="materialDetailVisible"
    :detail="materialDetail"
    :loading="materialDetailLoading"
  />

  <TransferDialog
    v-model:visible="transferVisible"
    :material="transferMaterial"
    :search-users="searchUsers"
    :user-options-loading="userOptionsLoading"
    :users="users"
    @done="afterMaterialChanged"
  />

  <WorkflowNodeSelectDialog ref="workflowNodeSelector" />
</template>
