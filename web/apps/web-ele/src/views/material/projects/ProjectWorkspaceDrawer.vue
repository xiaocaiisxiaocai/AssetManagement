<script lang="ts" setup>
import type { DeleteStatus } from './project-workspace-types';

import type {
  MaterialFlowItem,
  MaterialItem,
  MaterialStatus,
} from '#/api/material';
import type { TestProjectFollowup, TestProjectItem } from '#/api/test-project';

import { computed } from 'vue';

import { ElButton, ElDrawer, ElTabPane, ElTabs, ElTag } from 'element-plus';

import { formatDate } from '#/utils/date-format';

import { projectFollowUpStatusMeta } from './project-workspace-rules';
import ProjectFlowsTab from './ProjectFlowsTab.vue';
import ProjectFollowupsTab from './ProjectFollowupsTab.vue';
import ProjectMaterialsTab from './ProjectMaterialsTab.vue';
import ProjectTestingWorkspace from './testing/ProjectTestingWorkspace.vue';

type MaterialActionAccess = {
  canApprove: boolean;
  canDelete: boolean;
  canPurge: boolean;
  canRestore: boolean;
  canReturn: boolean;
  canTransfer: boolean;
};

const props = defineProps<{
  canCreate: boolean;
  canEdit: boolean;
  canUpdateProgress: boolean;
  currentProject: null | TestProjectItem;
  currentUserId: number;
  editingFollowupId: null | number;
  followupLoading: boolean;
  followups: TestProjectFollowup[];
  followupSaving: boolean;
  handledCount: number;
  handledFlows: MaterialFlowItem[];
  handledLoading: boolean;
  isSupervisor: boolean;
  materialAccess: MaterialActionAccess;
  materialLoading: boolean;
  materialPageSizeOptions: number[];
  materials: MaterialItem[];
  materialTotal: number;
  myCount: number;
  myFlows: MaterialFlowItem[];
  myLoading: boolean;
  pageSizeOptions: number[];
  pendingCount: number;
  pendingFlows: MaterialFlowItem[];
  pendingLoading: boolean;
  processingFlowIds: number[];
  readOnly: boolean;
}>();

const emit = defineEmits<{
  approve: [flow: MaterialFlowItem];
  command: [command: number | string, material: MaterialItem];
  create: [];
  detail: [material: MaterialItem];
  edit: [material: MaterialItem];
  flowPageChange: [];
  flowTabChange: [];
  followupCancelEdit: [];
  followupEdit: [followup: TestProjectFollowup];
  followupRemove: [followup: TestProjectFollowup];
  followupSave: [];
  handledPageSizeChange: [];
  materialPageChange: [];
  materialPageSizeChange: [];
  myPageSizeChange: [];
  pendingPageSizeChange: [];
  progress: [project: TestProjectItem];
  reject: [flow: MaterialFlowItem];
  reset: [];
  search: [];
  tabChange: [name: number | string];
  withdraw: [flow: MaterialFlowItem];
}>();
const visible = defineModel<boolean>('visible', { required: true });
const activeTab = defineModel<string>('activeTab', { required: true });
const flowActiveTab = defineModel<string>('flowActiveTab', { required: true });
const followupForm = defineModel<{ content: string }>('followupForm', {
  required: true,
});
const handledQuery = defineModel<{ page: number; pageSize: number }>(
  'handledQuery',
  { required: true },
);
const materialQuery = defineModel<{
  deleteStatus: DeleteStatus;
  materialNo: string;
  name: string;
  page: number;
  pageSize: number;
  statuses: MaterialStatus[];
}>('materialQuery', { required: true });
const myQuery = defineModel<{ page: number; pageSize: number }>('myQuery', {
  required: true,
});
const pendingQuery = defineModel<{ page: number; pageSize: number }>(
  'pendingQuery',
  { required: true },
);

const currentProjectCodeText = computed(
  () => props.currentProject?.code || '未设置编号',
);
const currentProjectTypeText = computed(
  () =>
    props.currentProject?.projectTypeLabel ||
    props.currentProject?.projectTypeCode ||
    '未分类',
);
const currentProjectProgressText = computed(
  () =>
    props.currentProject?.progressLabel ||
    props.currentProject?.progressCode ||
    '未设置进度',
);
function statusMeta(project: TestProjectItem) {
  return projectFollowUpStatusMeta(project);
}
</script>

<template>
  <ElDrawer v-model="visible" class="project-workspace-drawer" size="88%">
    <template #header>
      <div class="drawer-workspace-title">
        <strong>项目跟进工作台</strong>
      </div>
    </template>
    <div v-if="currentProject" class="followup-panel">
      <header class="workspace-project-heading">
        <div class="workspace-project-heading-main">
          <h2 class="project-title">{{ currentProject.name }}</h2>
          <div class="project-meta-line">
            <span>{{ currentProjectCodeText }}</span>
            <span>{{ currentProjectTypeText }}</span>
          </div>
        </div>
        <div class="workspace-project-heading-actions">
          <ElButton
            v-if="canUpdateProgress"
            size="small"
            @click="emit('progress', currentProject)"
          >
            更新进展
          </ElButton>
          <ElTag v-if="readOnly" type="info">已删除项目 · 只读归档</ElTag>
        </div>
      </header>

      <ProjectMaterialsTab
        v-model:query="materialQuery"
        :access="materialAccess"
        :can-create="canCreate"
        :can-edit="canEdit"
        :current-user-id="currentUserId"
        :is-supervisor="isSupervisor"
        :loading="materialLoading"
        :materials="materials"
        :page-size-options="materialPageSizeOptions"
        :project-owner-id="currentProject.ownerId"
        :total="materialTotal"
        @command="(command, row) => emit('command', command, row)"
        @create="emit('create')"
        @detail="emit('detail', $event)"
        @edit="emit('edit', $event)"
        @page-change="emit('materialPageChange')"
        @page-size-change="emit('materialPageSizeChange')"
        @reset="emit('reset')"
        @search="emit('search')"
      />

      <section aria-label="项目进度与跟进概况" class="project-brief">
        <div class="project-brief-stats">
          <div class="brief-stat">
            <span class="brief-stat-label">项目整体进度</span>
            <strong>{{ currentProjectProgressText }}</strong>
          </div>
          <div class="brief-stat">
            <span class="brief-stat-label">负责人</span>
            <strong>{{ currentProject.ownerName || '-' }}</strong>
          </div>
          <div class="brief-stat">
            <span class="brief-stat-label">下次跟进</span>
            <div class="brief-stat-value">
              <strong>{{
                formatDate(currentProject.nextFollowUpDueDate)
              }}</strong>
              <ElTag :type="statusMeta(currentProject).type" size="small">
                {{ statusMeta(currentProject).label }}
              </ElTag>
            </div>
          </div>
          <div class="brief-stat">
            <span class="brief-stat-label">跟进间隔</span>
            <strong>{{ currentProject.followUpIntervalDays }} 天</strong>
          </div>
          <div class="brief-stat">
            <span class="brief-stat-label">料件数</span>
            <strong>{{ currentProject.materialCount }} 件</strong>
          </div>
        </div>
      </section>

      <ElTabs
        v-model="activeTab"
        class="project-work-tabs"
        @tab-change="emit('tabChange', $event)"
      >
        <ElTabPane label="测试阶段与报告" name="stages">
          <ProjectTestingWorkspace
            :key="currentProject.id"
            :enabled="visible"
            :project-closed-date="currentProject.closedDate"
            :project-id="currentProject.id"
            :project-progress-code="currentProject.progressCode"
          />
        </ElTabPane>

        <ProjectFlowsTab
          v-model:active-tab="flowActiveTab"
          v-model:handled-query="handledQuery"
          v-model:my-query="myQuery"
          v-model:pending-query="pendingQuery"
          :can-approve="materialAccess.canApprove"
          :handled-count="handledCount"
          :handled-flows="handledFlows"
          :handled-loading="handledLoading"
          :my-count="myCount"
          :my-flows="myFlows"
          :my-loading="myLoading"
          :page-size-options="pageSizeOptions"
          :pending-count="pendingCount"
          :pending-flows="pendingFlows"
          :pending-loading="pendingLoading"
          :processing-flow-ids="processingFlowIds"
          :read-only="readOnly"
          @approve="emit('approve', $event)"
          @handled-page-size-change="emit('handledPageSizeChange')"
          @my-page-size-change="emit('myPageSizeChange')"
          @page-change="emit('flowPageChange')"
          @pending-page-size-change="emit('pendingPageSizeChange')"
          @reject="emit('reject', $event)"
          @tab-change="emit('flowTabChange')"
          @withdraw="emit('withdraw', $event)"
        />

        <ProjectFollowupsTab
          v-model:form="followupForm"
          :editing-id="editingFollowupId"
          :followups="followups"
          :loading="followupLoading"
          :project="currentProject"
          :saving="followupSaving"
          @cancel-edit="emit('followupCancelEdit')"
          @edit="emit('followupEdit', $event)"
          @remove="emit('followupRemove', $event)"
          @save="emit('followupSave')"
        />
      </ElTabs>
    </div>
  </ElDrawer>
</template>

<style scoped src="./ProjectWorkspaceDrawer.css"></style>
