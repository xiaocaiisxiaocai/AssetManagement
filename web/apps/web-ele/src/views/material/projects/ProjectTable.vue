<script lang="ts" setup>
import type { ProjectFilter } from './project-filter';
import type { DeleteStatus } from './project-workspace-types';

import type { TestProjectItem, TestProjectOption } from '#/api/test-project';

import {
  ElButton,
  ElInput,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { formatDate } from '#/utils/date-format';

import {
  canUpdateProjectProgress,
  projectFollowUpStatusMeta,
} from './project-workspace-rules';

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
  <div class="project-toolbar">
    <div class="project-toolbar-left">
      <ElInput
        v-model="filter.code"
        aria-label="项目编号"
        clearable
        placeholder="项目编号"
        style="width: 150px"
        @keyup.enter="emit('search')"
      />
      <ElInput
        v-model="filter.name"
        aria-label="项目名称"
        clearable
        placeholder="项目名称"
        style="width: 180px"
        @keyup.enter="emit('search')"
      />
      <ElSelect
        v-model="filter.projectTypeCode"
        aria-label="项目类型"
        clearable
        placeholder="项目类型"
        style="width: 130px"
      >
        <ElOption
          v-for="item in projectTypeOptions"
          :key="item.id"
          :label="item.label"
          :value="item.code"
        />
      </ElSelect>
      <ElSelect
        v-model="filter.ownerId"
        :loading="userOptionsLoading"
        :remote-method="(keyword: string) => emit('userSearch', keyword)"
        aria-label="负责人"
        clearable
        filterable
        placeholder="负责人"
        remote
        style="width: 150px"
      >
        <ElOption
          v-for="user in ownerOptions"
          :key="user.id"
          :label="user.name"
          :value="user.id"
        />
      </ElSelect>
      <ElSelect
        v-model="filter.progressCode"
        aria-label="项目进度"
        clearable
        placeholder="进度"
        style="width: 130px"
      >
        <ElOption
          v-for="item in progressOptions"
          :key="item.id"
          :label="item.label"
          :value="item.code"
        />
      </ElSelect>
      <ElSelect
        v-model="deleteStatus"
        aria-label="项目删除状态"
        placeholder="删除状态"
        style="width: 130px"
        @change="emit('statusChange')"
      >
        <ElOption label="全部" value="all" />
        <ElOption label="未删除" value="active" />
        <ElOption label="已删除" value="deleted" />
      </ElSelect>
      <ElButton type="primary" @click="emit('search')">查询</ElButton>
      <ElButton @click="emit('reset')">重置</ElButton>
    </div>
    <div class="project-toolbar-right">
      <ElButton v-if="access.canOption" @click="emit('options')">配置</ElButton>
      <ElButton v-if="access.canImport" @click="emit('import')">
        批量导入
      </ElButton>
      <ElButton v-if="access.canExport" @click="emit('export')">
        导出 Excel
      </ElButton>
      <ElButton v-if="access.canCreate" type="primary" @click="emit('create')">
        新增项目
      </ElButton>
    </div>
  </div>

  <div class="project-table-panel">
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
          <template v-if="!row.isDeleted">
            <ElButton
              v-if="access.canEdit && !row.closedDate"
              link
              size="small"
              type="primary"
              @click="emit('edit', row)"
            >
              编辑
            </ElButton>
            <ElButton
              v-else-if="canUpdateProjectProgress(row, currentUserId)"
              link
              size="small"
              type="primary"
              @click="emit('progress', row)"
            >
              更新进展
            </ElButton>
            <ElButton
              v-if="access.canDelete"
              link
              size="small"
              type="danger"
              @click="emit('remove', row)"
            >
              删除
            </ElButton>
          </template>
          <template v-else>
            <ElButton
              v-if="access.canRestore"
              link
              size="small"
              type="success"
              @click="emit('restore', row)"
            >
              撤销删除
            </ElButton>
            <ElButton
              v-if="access.canPurge"
              link
              size="small"
              type="danger"
              @click="emit('purge', row)"
            >
              彻底删除
            </ElButton>
          </template>
        </template>
      </ElTableColumn>
    </ElTable>
    <div class="table-bottom-pager">
      <div class="table-bottom-pager-left">
        <span>共 {{ filteredTotal }} 条记录</span>
        <span class="table-bottom-pager-divider">|</span>
        <span>每页</span>
        <ElSelect
          v-model="query.pageSize"
          aria-label="项目列表每页条数"
          style="width: 92px"
          @change="emit('pageSizeChange')"
        >
          <ElOption
            v-for="size in pageSizeOptions"
            :key="size"
            :label="`${size}`"
            :value="size"
          />
        </ElSelect>
      </div>
      <ElPagination
        v-model:current-page="query.page"
        :page-size="query.pageSize"
        :total="filteredTotal"
        background
        layout="prev, pager, next"
        @current-change="emit('pageChange')"
      />
    </div>
  </div>
</template>

<style scoped>
.project-toolbar {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.project-toolbar-left,
.project-toolbar-right {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
}

.project-toolbar-right {
  justify-content: flex-end;
}

.project-table-panel {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  background: var(--asset-page-surface);
  border: 1px solid var(--asset-page-border);
  border-radius: 12px;
  box-shadow: var(--asset-page-shadow);
}

.project-table-panel :deep(.el-table) {
  flex: 1;
  min-height: 0;
}

.table-bottom-pager {
  display: flex;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: var(--asset-page-surface);
  border-top: 1px solid var(--asset-page-border);
}

.table-bottom-pager-left {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  font-size: 14px;
  line-height: 20px;
  color: var(--asset-page-muted);
}

.table-bottom-pager-divider {
  color: var(--asset-page-border);
}

:deep(.project-row-deleted td.el-table__cell) {
  color: var(--el-text-color-disabled);
  background-color: var(--el-fill-color-light) !important;
}

@media (max-width: 768px) {
  .project-toolbar {
    align-items: stretch;
  }
}
</style>
