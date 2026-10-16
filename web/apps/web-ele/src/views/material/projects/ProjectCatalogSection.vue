<script lang="ts" setup>
import { useProjectPageContext } from './project-page-context';
import ProjectTable from './ProjectTable.vue';

const {
  currentUserId,
  deleteStatus,
  exportProjects,
  importDialogVisible,
  loadData,
  loading,
  onProjectPageSizeChange,
  openCreate,
  openEdit,
  openFollowups,
  openOptionDialog,
  openProgress,
  pageSizeOptions,
  projectActionAccess,
  projectFilterModel,
  projectOwnerOptions,
  projectQueryModel,
  projectTotal,
  projectTypeOptions,
  projects,
  progressOptions,
  purge,
  remove,
  resetProjectFilter,
  restore,
  searchProjects,
  searchUsers,
  userOptionsLoading,
} = useProjectPageContext();
</script>

<template>
  <ProjectTable
    v-model:delete-status="deleteStatus"
    v-model:filter="projectFilterModel"
    v-model:query="projectQueryModel"
    :access="projectActionAccess"
    :current-user-id="currentUserId"
    :filtered-total="projectTotal"
    :loading="loading"
    :owner-options="projectOwnerOptions"
    :page-size-options="pageSizeOptions"
    :paged-projects="projects"
    :progress-options="progressOptions"
    :project-type-options="projectTypeOptions"
    :user-options-loading="userOptionsLoading"
    @create="openCreate"
    @edit="openEdit"
    @export="exportProjects"
    @import="importDialogVisible = true"
    @open="openFollowups"
    @options="openOptionDialog"
    @page-change="loadData"
    @page-size-change="onProjectPageSizeChange"
    @progress="openProgress"
    @purge="purge"
    @remove="remove"
    @reset="resetProjectFilter"
    @restore="restore"
    @search="searchProjects"
    @user-search="searchUsers"
  />
</template>
