<script lang="ts" setup>
import type { ProjectFlowListKind } from './project-flow-actions';
import type { DeleteStatus } from './project-workspace-types';

import type {
  MaterialFlowItem,
  MaterialItem,
  MaterialStatus,
} from '#/api/material';
import type { TestProjectFollowup, TestProjectItem } from '#/api/test-project';

import { computed, onBeforeUnmount, ref, watch } from 'vue';

import { useMediaQuery } from '@vueuse/core';
import {
  ElButton,
  ElDrawer,
  ElSplitter,
  ElSplitterPanel,
  ElTag,
} from 'element-plus';

import {
  collapsedSectionPanelSize,
  ratioAt,
  readWorkspaceLayout,
  toPercent,
  writeWorkspaceLayout,
} from './project-workspace-layout';
import {
  buildWorkspaceReportHtml,
  loadWorkspaceReportData,
  printWorkspaceReport,
  stopWorkspaceReport,
  workspaceReportTitle,
} from './project-workspace-report';
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
  /** 账号是否有审批权限，与项目阶段无关；能否处理仍看 materialAccess.canApprove。 */
  canViewApprovals: boolean;
  currentProject: null | TestProjectItem;
  currentUserId: number;
  editingFollowupId: null | number;
  flowInitialTab?: ProjectFlowListKind;
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
  receivingCount: number;
  receivingFlows: MaterialFlowItem[];
  receivingLoading: boolean;
}>();

const emit = defineEmits<{
  approve: [flow: MaterialFlowItem];
  command: [command: number | string, material: MaterialItem];
  confirm: [flow: MaterialFlowItem];
  create: [];
  detail: [material: MaterialItem];
  edit: [material: MaterialItem];
  flowPageChange: [kind: ProjectFlowListKind];
  followupCancelEdit: [];
  followupEdit: [followup: TestProjectFollowup];
  followupSave: [];
  handledPageSizeChange: [];
  materialPageChange: [];
  materialPageSizeChange: [];
  myPageSizeChange: [];
  pendingPageSizeChange: [];
  progress: [project: TestProjectItem];
  receivingPageSizeChange: [];
  reject: [flow: MaterialFlowItem];
  release: [flow: MaterialFlowItem];
  reset: [];
  search: [];
  withdraw: [flow: MaterialFlowItem];
}>();
const visible = defineModel<boolean>('visible', { required: true });
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
const receivingQuery = defineModel<{ page: number; pageSize: number }>(
  'receivingQuery',
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

const layout = readWorkspaceLayout();
const materialsCollapsed = ref(false);
const collapsedSectionHeight = ref(0);
const materialsSize = ref<number | string>(toPercent(layout.materials));
const testingSize = ref<number | string>(toPercent(layout.testing));
const flowsSize = ref<number | string>(toPercent(layout.flows));
const narrowLayout = useMediaQuery('(max-width: 1100px)');
const bottomPanelClass = computed(() =>
  narrowLayout.value ? 'workspace-panel-v' : 'workspace-panel-h',
);

const materialsPanelSize = computed(() =>
  materialsCollapsed.value && collapsedSectionHeight.value > 0
    ? collapsedSectionPanelSize(collapsedSectionHeight.value)
    : materialsSize.value,
);

function onCollapsedSectionHeight(height: number) {
  collapsedSectionHeight.value = height;
}

function onMaterialsSizeUpdate(size: number | string) {
  if (!materialsCollapsed.value) materialsSize.value = size;
}

function onVerticalResizeEnd(_index: number, pxSizes: number[]) {
  const testing = ratioAt(pxSizes, 1);
  if (testing !== undefined) layout.testing = testing;
  const materials = materialsCollapsed.value ? undefined : ratioAt(pxSizes, 0);
  if (materials !== undefined) layout.materials = materials;
  writeWorkspaceLayout(layout);
}

function onBottomResizeEnd(_index: number, pxSizes: number[]) {
  const flows = ratioAt(pxSizes, 0);
  if (flows === undefined) return;
  layout.flows = flows;
  writeWorkspaceLayout(layout);
}

const exportingReport = ref(false);

async function exportReport() {
  const project = props.currentProject;
  if (!project || exportingReport.value) return;
  exportingReport.value = true;
  try {
    const data = await loadWorkspaceReportData(project);
    // 取数期间关闭了抽屉或切换了项目，则不再弹出打印。
    if (!visible.value || props.currentProject?.id !== project.id) return;
    printWorkspaceReport(
      buildWorkspaceReportHtml(data),
      workspaceReportTitle(project.name, project.code, data.generatedAt),
    );
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    exportingReport.value = false;
  }
}

watch(visible, (open) => {
  if (!open) stopWorkspaceReport();
});
onBeforeUnmount(() => {
  stopWorkspaceReport();
});
</script>

<template>
  <ElDrawer
    v-model="visible"
    :close-on-click-modal="!followupSaving"
    :close-on-press-escape="!followupSaving"
    :show-close="!followupSaving"
    class="project-workspace-drawer"
    size="88%"
  >
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
            <span class="project-meta-progress">
              {{ currentProjectProgressText }}
            </span>
            <span>负责人：{{ currentProject.ownerName || '-' }}</span>
          </div>
        </div>
        <div class="workspace-project-heading-actions">
          <ElButton
            :loading="exportingReport"
            size="small"
            @click="exportReport"
          >
            导出页面
          </ElButton>
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

      <ElSplitter
        class="workspace-splitter"
        layout="vertical"
        @resize-end="onVerticalResizeEnd"
      >
        <ElSplitterPanel
          :min="materialsCollapsed ? undefined : 120"
          :resizable="!materialsCollapsed"
          :size="materialsPanelSize"
          class="workspace-panel-v is-first"
          @update:size="onMaterialsSizeUpdate"
        >
          <ProjectMaterialsTab
            v-model:collapsed="materialsCollapsed"
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
            @collapsed-height="onCollapsedSectionHeight"
            @command="(command, row) => emit('command', command, row)"
            @create="emit('create')"
            @detail="emit('detail', $event)"
            @edit="emit('edit', $event)"
            @page-change="emit('materialPageChange')"
            @page-size-change="emit('materialPageSizeChange')"
            @reset="emit('reset')"
            @search="emit('search')"
          />
        </ElSplitterPanel>

        <ElSplitterPanel
          v-model:size="testingSize"
          :min="140"
          class="workspace-panel-v"
        >
          <ProjectTestingWorkspace
            :key="currentProject.id"
            :enabled="visible"
            :project-closed-date="currentProject.closedDate"
            :project-id="currentProject.id"
            :project-progress-code="currentProject.progressCode"
          />
        </ElSplitterPanel>

        <ElSplitterPanel :min="200" class="workspace-panel-v is-last">
          <ElSplitter
            :layout="narrowLayout ? 'vertical' : 'horizontal'"
            class="workspace-bottom-splitter"
            @resize-end="onBottomResizeEnd"
          >
            <ElSplitterPanel
              v-model:size="flowsSize"
              :class="bottomPanelClass"
              :min="narrowLayout ? 160 : 420"
              class="is-first"
            >
              <ProjectFlowsTab
                :key="currentProject.id"
                v-model:handled-query="handledQuery"
                v-model:my-query="myQuery"
                v-model:pending-query="pendingQuery"
                v-model:receiving-query="receivingQuery"
                :can-handle="materialAccess.canApprove && !readOnly"
                :can-view-approvals="canViewApprovals"
                :handled-count="handledCount"
                :handled-flows="handledFlows"
                :handled-loading="handledLoading"
                :initial-tab="flowInitialTab"
                :my-count="myCount"
                :my-flows="myFlows"
                :my-loading="myLoading"
                :page-size-options="pageSizeOptions"
                :pending-count="pendingCount"
                :pending-flows="pendingFlows"
                :pending-loading="pendingLoading"
                :processing-flow-ids="processingFlowIds"
                :read-only="readOnly"
                :receiving-count="receivingCount"
                :receiving-flows="receivingFlows"
                :receiving-loading="receivingLoading"
                @approve="emit('approve', $event)"
                @confirm="emit('confirm', $event)"
                @handled-page-size-change="emit('handledPageSizeChange')"
                @my-page-size-change="emit('myPageSizeChange')"
                @page-change="emit('flowPageChange', $event)"
                @pending-page-size-change="emit('pendingPageSizeChange')"
                @receiving-page-size-change="emit('receivingPageSizeChange')"
                @reject="emit('reject', $event)"
                @release="emit('release', $event)"
                @withdraw="emit('withdraw', $event)"
              />
            </ElSplitterPanel>

            <ElSplitterPanel
              :class="bottomPanelClass"
              :min="narrowLayout ? 160 : 320"
              class="is-last"
            >
              <ProjectFollowupsTab
                v-model:form="followupForm"
                :editing-id="editingFollowupId"
                :followups="followups"
                :loading="followupLoading"
                :project="currentProject"
                :saving="followupSaving"
                @cancel-edit="emit('followupCancelEdit')"
                @edit="emit('followupEdit', $event)"
                @save="emit('followupSave')"
              />
            </ElSplitterPanel>
          </ElSplitter>
        </ElSplitterPanel>
      </ElSplitter>
    </div>
  </ElDrawer>
</template>

<style scoped src="./ProjectWorkspaceDrawer.css"></style>
