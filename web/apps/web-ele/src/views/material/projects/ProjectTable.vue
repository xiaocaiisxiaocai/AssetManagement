<script lang="ts" setup>
import type { ProjectFilter } from './project-filter';
import type { DeleteStatus } from './project-workspace-types';

import type { TestProjectItem, TestProjectOption } from '#/api/test-project';

import { ElButton, ElTable, ElTableColumn, ElTag } from 'element-plus';

import { formatDate } from '#/utils/date-format';

import { projectFollowUpStatusMeta } from './project-workspace-rules';
import ProjectTablePager from './ProjectTablePager.vue';
import ProjectTableRowActions from './ProjectTableRowActions.vue';
import ProjectTableToolbar from './ProjectTableToolbar.vue';

type ProjectActionAccess = {
  canCreate: boolean;
  canDelete: boolean;
  canEdit: boolean;
  canExport: boolean;
  canImport: boolean;
  canOption: boolean;
  canPurge: boolean;
  canRestore: boolean;
};

defineProps<{
  access: ProjectActionAccess;
  currentUserId: number;
  filteredTotal: number;
  loading: boolean;
  ownerOptions: { id: number; name: string }[];
  pagedProjects: TestProjectItem[];
  pageSizeOptions: number[];
  progressOptions: TestProjectOption[];
  projectTypeOptions: TestProjectOption[];
  userOptionsLoading?: boolean;
}>();

const emit = defineEmits<{
  create: [];
  edit: [project: TestProjectItem];
  export: [];
  import: [];
  open: [project: TestProjectItem];
  options: [];
  pageChange: [];
  pageSizeChange: [];
  progress: [project: TestProjectItem];
  purge: [project: TestProjectItem];
  remove: [project: TestProjectItem];
  reset: [];
  restore: [project: TestProjectItem];
  search: [];
  statusChange: [];
  userSearch: [keyword: string];
}>();
const filter = defineModel<ProjectFilter>('filter', { required: true });
const query = defineModel<{ page: number; pageSize: number }>('query', {
  required: true,
});
const deleteStatus = defineModel<DeleteStatus>('deleteStatus', {
  required: true,
});

function optionalText(value?: null | string) {
  return value && value.trim() ? value : '-';
}

function tableRowClassName({ row }: { row: TestProjectItem }) {
  return row.isDeleted ? 'project-row-deleted' : '';
}
</script>

<template>
  <ProjectTableToolbar
    v-model:delete-status="deleteStatus"
    v-model:filter="filter"
    :access="access"
    :owner-options="ownerOptions"
    :progress-options="progressOptions"
    :project-type-options="projectTypeOptions"
    :user-options-loading="userOptionsLoading"
    @create="emit('create')"
    @export="emit('export')"
    @import="emit('import')"
    @options="emit('options')"
    @reset="emit('reset')"
    @search="emit('search')"
    @status-change="emit('statusChange')"
    @user-search="emit('userSearch', $event)"
  />

  <div class="project-table-panel page-fill">
    <ElTable
      :data="pagedProjects"
      :row-class-name="tableRowClassName"
      border
      height="100%"
      scrollbar-always-on
      stripe
      v-loading="loading"
    >
      <ElTableColumn
        fixed
        label="项目编号"
        min-width="130"
        prop="code"
        show-overflow-tooltip
      />
      <ElTableColumn label="项目名称" min-width="150" show-overflow-tooltip>
        <template #default="{ row }">
          <ElButton link type="primary" @click="emit('open', row)">
            {{ row.name }}
          </ElButton>
        </template>
      </ElTableColumn>
      <ElTableColumn label="项目类型" min-width="120">
        <template #default="{ row }">
          {{ row.projectTypeLabel || row.projectTypeCode || '-' }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="负责人" min-width="110">
        <template #default="{ row }">{{ row.ownerName || '-' }}</template>
      </ElTableColumn>
      <ElTableColumn align="center" label="开始时间" width="110">
        <template #default="{ row }">
          {{ formatDate(row.startDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="计划完成" width="110">
        <template #default="{ row }">
          {{ formatDate(row.plannedFinishDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="结案时间" width="110">
        <template #default="{ row }">
          {{ formatDate(row.closedDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="进度" min-width="110">
        <template #default="{ row }">
          <ElTag size="small" type="info">
            {{ row.progressLabel || row.progressCode || '-' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="测试情况" min-width="120" show-overflow-tooltip>
        <template #default="{ row }">
          {{ optionalText(row.testStatus) }}
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="下次跟进" width="130">
        <template #default="{ row }">
          <div>{{ formatDate(row.nextFollowUpDueDate) }}</div>
          <ElTag :type="projectFollowUpStatusMeta(row).type" size="small">
            {{ projectFollowUpStatusMeta(row).label }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="间隔" width="80">
        <template #default="{ row }">
          {{ row.followUpIntervalDays }}天
        </template>
      </ElTableColumn>
      <ElTableColumn
        align="center"
        label="料件数"
        prop="materialCount"
        width="80"
      />
      <ElTableColumn align="center" label="状态" width="90">
        <template #default="{ row }">
          <ElTag v-if="row.isDeleted" size="small" type="danger">
            已删除
          </ElTag>
          <ElTag v-else size="small" type="success">正常</ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" fixed="right" label="操作" width="160">
        <template #default="{ row }">
          <ProjectTableRowActions
            :access="access"
            :current-user-id="currentUserId"
            :row="row"
            @edit="emit('edit', row)"
            @progress="emit('progress', row)"
            @purge="emit('purge', row)"
            @remove="emit('remove', row)"
            @restore="emit('restore', row)"
          />
        </template>
      </ElTableColumn>
    </ElTable>
    <ProjectTablePager
      v-model:query="query"
      :page-size-options="pageSizeOptions"
      :total="filteredTotal"
      @page-change="emit('pageChange')"
      @page-size-change="emit('pageSizeChange')"
    />
  </div>
</template>

<style scoped src="./ProjectTable.css"></style>
