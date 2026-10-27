<script lang="ts" setup>
import type { ProjectFilter } from './project-filter';
import type { DeleteStatus } from './project-workspace-types';

import type { TestProjectItem, TestProjectOption } from '#/api/test-project';
import type { TableColumnOption } from '#/components/table/column-visibility';
import type { HeaderFilterValue } from '#/components/table/header-multi-filter';

import { computed } from 'vue';

import { ElButton, ElEmpty, ElTable, ElTableColumn, ElTag } from 'element-plus';

import {
  deleteFlagsFromStatus,
  deleteStatusFromFlags,
} from '#/components/table/header-multi-filter';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';
import { useTableColumns } from '#/components/table/column-visibility';
import HeaderMultiFilter from '#/components/table/HeaderMultiFilter.vue';
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
  canManage: boolean;
  canOption: boolean;
  canPurge: boolean;
  canRestore: boolean;
};

const props = defineProps<{
  access: ProjectActionAccess;
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
  reopen: [project: TestProjectItem];
  reset: [];
  restore: [project: TestProjectItem];
  search: [];
  userSearch: [keyword: string];
}>();
const filter = defineModel<ProjectFilter>('filter', { required: true });
const query = defineModel<{ page: number; pageSize: number }>('query', {
  required: true,
});
const deleteStatus = defineModel<DeleteStatus>('deleteStatus', {
  required: true,
});

const projectTypeFilterOptions = computed(() =>
  props.projectTypeOptions.map((item) => ({
    label: item.label,
    value: item.code,
  })),
);
const progressFilterOptions = computed(() =>
  props.progressOptions.map((item) => ({
    label: item.label,
    value: item.code,
  })),
);
const ownerFilterOptions = computed(() =>
  props.ownerOptions.map((item) => ({ label: item.name, value: item.id })),
);
const deleteHeader = computed<HeaderFilterValue[]>({
  get: () => deleteFlagsFromStatus(deleteStatus.value),
  set: (values) => {
    deleteStatus.value = deleteStatusFromFlags(values);
  },
});
const deleteGroups = [
  {
    options: [
      { label: '未删除', value: 'active' },
      { label: '已删除', value: 'deleted' },
    ],
  },
];

function optionalText(value?: null | string) {
  return value && value.trim() ? value : '-';
}

function tableRowClassName({ row }: { row: TestProjectItem }) {
  return row.isDeleted ? 'project-row-deleted' : '';
}

const hasProjectFilters = computed(() => {
  return Boolean(
    filter.value.code.trim() ||
      filter.value.name.trim() ||
      filter.value.ownerIds.length > 0 ||
      filter.value.progressCodes.length > 0 ||
      filter.value.projectTypeCodes.length > 0,
  );
});

const projectEmptyText = computed(() => {
  const hasFilters = hasProjectFilters.value;
  return hasFilters
    ? '没有符合条件的项目，请清空筛选后重试'
    : '暂无测试项目，可新增项目';
});

const tableColumns: TableColumnOption[] = [
  { key: 'code', label: '项目编号' },
  { key: 'name', label: '项目名称' },
  { key: 'projectType', label: '项目类型' },
  { key: 'owner', label: '负责人' },
  { key: 'startDate', label: '开始时间' },
  { key: 'plannedFinish', label: '计划完成' },
  { key: 'closedDate', label: '结案时间' },
  { key: 'progress', label: '进度' },
  { key: 'summary', label: '项目摘要' },
  { key: 'nextFollowUp', label: '下次跟进' },
  { key: 'interval', label: '间隔' },
  { key: 'materialCount', label: '料件数' },
  { key: 'status', label: '状态' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('project-list', tableColumns, (key, visible) => {
    if (visible) return;
    let changed = false;
    if (key === 'projectType' && filter.value.projectTypeCodes.length > 0) {
      filter.value.projectTypeCodes = [];
      changed = true;
    }
    if (key === 'owner' && filter.value.ownerIds.length > 0) {
      filter.value.ownerIds = [];
      changed = true;
    }
    if (key === 'progress' && filter.value.progressCodes.length > 0) {
      filter.value.progressCodes = [];
      changed = true;
    }
    if (key === 'status' && deleteStatus.value !== 'all') {
      deleteStatus.value = 'all';
      changed = true;
    }
    if (changed) emit('search');
  });
</script>

<template>
  <header class="project-page-heading">
    <div>
      <h1>测试项目</h1>
    </div>
  </header>
  <ProjectTableToolbar
    v-model:filter="filter"
    :access="access"
    @create="emit('create')"
    @export="emit('export')"
    @import="emit('import')"
    @options="emit('options')"
    @reset="emit('reset')"
    @search="emit('search')"
  >
    <template #tools>
      <TableColumnPicker
        :columns="tableColumns"
        :hidden-keys="hiddenKeys"
        @reset="resetColumns"
        @toggle="setColumnVisible"
      />
    </template>
  </ProjectTableToolbar>

  <div class="project-table-panel page-fill table-scroll">
    <ElTable
      :data="pagedProjects"
      :empty-text="projectEmptyText"
      :row-class-name="tableRowClassName"
      border
      height="100%"
      scrollbar-always-on
      stripe
      v-loading="loading"
    >
      <template #empty>
        <ElEmpty :description="projectEmptyText">
          <ElButton v-if="hasProjectFilters" @click="emit('reset')">
            清空筛选
          </ElButton>
        </ElEmpty>
      </template>
      <ElTableColumn
        v-if="visibleColumn('code')"
        fixed
        label="项目编号"
        min-width="130"
        prop="code"
        show-overflow-tooltip
      />
      <ElTableColumn
        v-if="visibleColumn('name')"
        label="项目名称"
        min-width="150"
        show-overflow-tooltip
      >
        <template #default="{ row }">
          <ElButton link type="primary" @click="emit('open', row)">
            {{ row.name }}
          </ElButton>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('projectType')"
        class-name="hide-on-mobile"
        label="项目类型"
        min-width="130"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="filter.projectTypeCodes"
            :options="projectTypeFilterOptions"
            filterable
            label="项目类型"
            @change="emit('search')"
          />
        </template>
        <template #default="{ row }">
          {{ row.projectTypeLabel || row.projectTypeCode || '-' }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('owner')"
        class-name="hide-on-mobile"
        label="负责人"
        min-width="130"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="filter.ownerIds"
            :loading="userOptionsLoading"
            :options="ownerFilterOptions"
            filterable
            label="负责人"
            remote
            @change="emit('search')"
            @search="emit('userSearch', $event)"
          />
        </template>
        <template #default="{ row }">{{ row.ownerName || '-' }}</template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('startDate')"
        align="center"
        class-name="hide-on-mobile"
        label="开始时间"
        width="110"
      >
        <template #default="{ row }">
          {{ formatDate(row.startDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('plannedFinish')"
        align="center"
        class-name="hide-on-mobile"
        label="计划完成"
        width="110"
      >
        <template #default="{ row }">
          {{ formatDate(row.plannedFinishDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('closedDate')"
        align="center"
        class-name="hide-on-mobile"
        label="结案时间"
        width="110"
      >
        <template #default="{ row }">
          {{ formatDate(row.closedDate) }}
        </template>
      </ElTableColumn>
      <ElTableColumn v-if="visibleColumn('progress')" label="进度" min-width="120">
        <template #header>
          <HeaderMultiFilter
            v-model="filter.progressCodes"
            :options="progressFilterOptions"
            filterable
            label="进度"
            @change="emit('search')"
          />
        </template>
        <template #default="{ row }">
          <ElTag size="small" type="info">
            {{ row.progressLabel || row.progressCode || '-' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('summary')"
        class-name="hide-on-mobile"
        label="项目摘要"
        min-width="120"
        show-overflow-tooltip
      >
        <template #default="{ row }">
          {{ optionalText(row.testStatus) }}
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('nextFollowUp')"
        align="center"
        label="下次跟进"
        width="130"
      >
        <template #default="{ row }">
          <div v-if="!row.isDeleted">
            {{ formatDate(row.nextFollowUpDueDate) }}
          </div>
          <ElTag :type="projectFollowUpStatusMeta(row).type" size="small">
            {{ projectFollowUpStatusMeta(row).label }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('interval')"
        align="center"
        class-name="hide-on-mobile"
        label="间隔"
        width="80"
      >
        <template #default="{ row }">
          {{ row.followUpIntervalDays }}天
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('materialCount')"
        align="center"
        class-name="hide-on-mobile"
        label="料件数"
        prop="materialCount"
        width="80"
      />
      <ElTableColumn
        v-if="visibleColumn('status')"
        align="center"
        label="状态"
        width="110"
      >
        <template #header>
          <HeaderMultiFilter
            v-model="deleteHeader"
            :groups="deleteGroups"
            label="状态"
            @change="emit('search')"
          />
        </template>
        <template #default="{ row }">
          <ElTag v-if="row.isDeleted" size="small" type="danger">
            已删除
          </ElTag>
          <ElTag v-else size="small" type="success">正常</ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        fixed="right"
        label="操作"
        width="220"
      >
        <template #default="{ row }">
          <ProjectTableRowActions
            :row="row"
            @edit="emit('edit', row)"
            @progress="emit('progress', row)"
            @purge="emit('purge', row)"
            @remove="emit('remove', row)"
            @reopen="emit('reopen', row)"
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
