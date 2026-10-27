<script lang="ts" setup>
import type {
  ProjectStageDto,
  ProjectStageStatus,
} from '#/api/project-workspace';
import type { TableColumnOption } from '#/components/table/column-visibility';

import { computed } from 'vue';

import {
  ElButton,
  ElEmpty,
  ElResult,
  ElSkeleton,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';

import {
  dateOnly,
  isProjectClosed,
  isProjectTesting,
  stageBlocksLanding,
} from './project-testing-rules';
import ProjectReportDocuments from './ProjectReportDocuments.vue';
import ProjectStageDialog from './ProjectStageDialog.vue';
import { useProjectTestingWorkspace } from './use-project-testing-workspace';

defineOptions({ name: 'ProjectTestingWorkspace' });

const props = defineProps<{
  enabled: boolean;
  projectClosedDate?: null | string;
  projectId: number;
  projectProgressCode?: null | string;
}>();

const {
  canCreateStage,
  canExport,
  canManage,
  deletingDocumentId,
  deletingStageId,
  downloadDocument,
  downloadingKey,
  editingStage,
  loadError,
  loading,
  loadWorkspace,
  openCreateStage,
  openEditStage,
  removeDocument,
  removeStage,
  saveStage,
  savingStage,
  stageDialogVisible,
  stageForm,
  uploadDocument,
  uploadingDocument,
  uploadingStageId,
  workspace,
  writeBusy,
} = useProjectTestingWorkspace(props);

const orderedStages = computed(() =>
  [...(workspace.value?.stages ?? [])].sort(
    (left, right) => left.sequence - right.sequence,
  ),
);
const currentStage = computed(
  () =>
    orderedStages.value.find(
      (stage) => stage.status === 'in_progress' || stage.status === 'blocked',
    ) ?? null,
);
const completedCount = computed(
  () =>
    orderedStages.value.filter((stage) => stage.status === 'completed').length,
);
const projectClosed = computed(() =>
  isProjectClosed(props.projectProgressCode, props.projectClosedDate),
);
const landingNeedsStageFinish = computed(
  () =>
    (props.projectProgressCode ?? '').toLowerCase() === 'landing' &&
    orderedStages.value.some((stage) => stageBlocksLanding(stage)),
);
const testingLocked = computed(() => {
  if (projectClosed.value) return true;
  if (landingNeedsStageFinish.value) return false;
  return !['planning', 'testing'].includes(
    (props.projectProgressCode ?? '').toLowerCase(),
  );
});
const finalStageId = computed(() => {
  if (!projectClosed.value || orderedStages.value.length === 0) return null;
  return orderedStages.value[orderedStages.value.length - 1]?.id ?? null;
});

const statusMap: Record<
  ProjectStageStatus,
  { label: string; type: 'danger' | 'info' | 'success' | 'warning' }
> = {
  blocked: { label: '受阻', type: 'danger' },
  completed: { label: '已完成', type: 'success' },
  in_progress: { label: '进行中', type: 'warning' },
  planned: { label: '计划中', type: 'info' },
};

function statusMeta(status: ProjectStageStatus) {
  return statusMap[status] ?? statusMap.planned;
}

function displayText(value?: null | string) {
  return value?.trim() || '—';
}

function dateRange(stage: ProjectStageDto) {
  if (!stage.startDate && !stage.endDate) return '—';
  return `${dateOnly(stage.startDate) || '—'} 至 ${dateOnly(stage.endDate) || '—'}`;
}

const stageColumns: TableColumnOption[] = [
  { key: 'seq', label: '阶段序号' },
  { key: 'name', label: '阶段名称' },
  { key: 'goal', label: '目标' },
  { key: 'content', label: '测试内容' },
  { key: 'result', label: '测试结果' },
  { key: 'dates', label: '起止日期' },
  { key: 'status', label: '状态' },
  { key: 'report', label: '报告附件' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('project-stages', stageColumns);
</script>

<template>
  <template v-if="enabled">
    <section
      aria-label="测试阶段与报告"
      class="workspace-section workspace-testing-section"
    >
      <div class="workspace-section-heading">
        <h3>测试阶段与报告</h3>
        <div v-if="workspace" class="testing-summary-inline">
          <div class="testing-summary-item testing-summary-current">
            <span>当前阶段</span>
            <strong>
              {{
                testingLocked
                  ? '测试阶段已锁定'
                  : currentStage
                    ? `${currentStage.sequence}. ${currentStage.name}`
                    : '暂无进行中阶段'
              }}
            </strong>
            <ElTag v-if="landingNeedsStageFinish" size="small" type="warning">
              阶段未完成，不能落地跟进
            </ElTag>
            <ElTag v-else-if="testingLocked" size="small" type="info">
              {{
                props.projectClosedDate ||
                props.projectProgressCode?.toLowerCase() === 'closed'
                  ? '已结案'
                  : '落地跟进'
              }}
            </ElTag>
            <ElTag
              v-else-if="currentStage"
              :type="statusMap[currentStage.status].type"
              size="small"
            >
              {{ statusMap[currentStage.status].label }}
            </ElTag>
          </div>
          <div class="testing-summary-item">
            <span>阶段进度</span>
            <strong>{{ completedCount }} / {{ orderedStages.length }}</strong>
          </div>
        </div>
        <div v-if="workspace" class="workspace-section-actions">
          <ElTag v-if="!canManage" size="small" type="info">只读</ElTag>
          <ElButton
            v-if="canCreateStage"
            :disabled="writeBusy"
            type="primary"
            @click="openCreateStage"
          >
            新增阶段
          </ElButton>
          <TableColumnPicker
            :columns="stageColumns"
            :hidden-keys="hiddenKeys"
            @reset="resetColumns"
            @toggle="setColumnVisible"
          />
        </div>
      </div>

      <div class="workspace-section-body">
        <div v-if="loading && !workspace" class="testing-loading-panel">
          <ElSkeleton :rows="4" animated />
        </div>

        <ElResult
          v-else-if="loadError"
          :sub-title="loadError"
          class="testing-load-error"
          icon="error"
          title="测试阶段与报告加载失败"
        >
          <template #extra>
            <ElButton :loading="loading" type="primary" @click="loadWorkspace">
              重新加载
            </ElButton>
          </template>
        </ElResult>

        <div
          v-else-if="workspace"
          class="stage-table-wrap workspace-fill-table workspace-stage-table"
          v-loading="loading"
        >
          <ElTable
            v-if="orderedStages.length > 0"
            :data="orderedStages"
            border
            class="compact-table stage-ledger-table"
            empty-text="暂无测试阶段"
            height="100%"
            row-key="id"
            table-layout="fixed"
          >
            <ElTableColumn
              v-if="visibleColumn('seq')"
              align="center"
              label="阶段序号"
              width="88"
            >
              <template #default="{ row }">{{ row.sequence }}</template>
            </ElTableColumn>
            <ElTableColumn
              v-if="visibleColumn('name')"
              align="center"
              label="阶段名称"
              min-width="150"
            >
              <template #default="{ row }">
                <div class="stage-name-content">
                  <span :title="row.name" class="stage-name-cell">{{
                    row.name
                  }}</span>
                  <ElTag
                    v-if="row.id === finalStageId"
                    size="small"
                    type="success"
                  >
                    结案阶段
                  </ElTag>
                </div>
              </template>
            </ElTableColumn>
            <ElTableColumn
              v-if="visibleColumn('goal')"
              align="center"
              label="目标"
              min-width="180"
            >
              <template #default="{ row }">
                <span :title="row.objective || ''" class="multiline-preview">
                  {{ displayText(row.objective) }}
                </span>
              </template>
            </ElTableColumn>
            <ElTableColumn
              v-if="visibleColumn('content')"
              align="center"
              label="测试内容"
              min-width="220"
            >
              <template #default="{ row }">
                <span :title="row.testContent || ''" class="multiline-preview">
                  {{ displayText(row.testContent) }}
                </span>
              </template>
            </ElTableColumn>
            <ElTableColumn
              v-if="visibleColumn('result')"
              align="center"
              label="测试结果"
              min-width="240"
            >
              <template #default="{ row }">
                <span :title="row.testResult || ''" class="multiline-preview">
                  {{ displayText(row.testResult) }}
                </span>
              </template>
            </ElTableColumn>
            <ElTableColumn
              v-if="visibleColumn('dates')"
              align="center"
              label="起止日期"
              width="190"
            >
              <template #default="{ row }">{{ dateRange(row) }}</template>
            </ElTableColumn>
            <ElTableColumn
              v-if="visibleColumn('status')"
              align="center"
              label="状态"
              width="96"
            >
              <template #default="{ row }">
                <ElTag :type="statusMeta(row.status).type" size="small">
                  {{ statusMeta(row.status).label }}
                </ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn
              v-if="visibleColumn('report')"
              align="center"
              fixed="right"
              label="报告附件"
              width="210"
            >
              <template #default="{ row }">
                <ProjectReportDocuments
                  :key="row.id"
                  :can-download="canExport"
                  :can-manage="canManage"
                  :deleting-document-id="deletingDocumentId"
                  :documents="workspace.documents"
                  :downloading-key="downloadingKey"
                  :scope-name="`第${row.sequence}阶段：${row.name}`"
                  :stage-id="row.id"
                  :uploading="uploadingDocument && uploadingStageId === row.id"
                  :write-busy="writeBusy"
                  @download="downloadDocument"
                  @remove="removeDocument"
                  @upload="uploadDocument"
                />
              </template>
            </ElTableColumn>
            <ElTableColumn
              v-if="canManage && visibleColumn('actions')"
              align="center"
              class-name="table-action-cell"
              fixed="right"
              label="操作"
              width="120"
            >
              <template #default="{ row }">
                <div class="centered-actions stage-row-actions">
                  <ElButton
                    v-if="canManage"
                    :disabled="writeBusy"
                    link
                    size="small"
                    type="primary"
                    @click="openEditStage(row)"
                  >
                    编辑
                  </ElButton>
                  <ElButton
                    v-if="canManage"
                    :disabled="writeBusy && deletingStageId !== row.id"
                    :loading="deletingStageId === row.id"
                    link
                    size="small"
                    type="danger"
                    @click="removeStage(row)"
                  >
                    删除
                  </ElButton>
                </div>
              </template>
            </ElTableColumn>
          </ElTable>
          <ElEmpty
            v-else
            :description="
              testingLocked
                ? '测试阶段已锁定，仅保留历史记录'
                : isProjectTesting(props.projectProgressCode)
                  ? canCreateStage
                    ? '暂无测试阶段，可新增第一阶段'
                    : '暂无测试阶段'
                  : '进入测试中后可维护测试阶段'
            "
            :image-size="48"
          />
        </div>
      </div>
    </section>

    <ProjectStageDialog
      v-model:form="stageForm"
      v-model:visible="stageDialogVisible"
      :editing="Boolean(editingStage)"
      :saving="savingStage"
      @save="saveStage"
    />
  </template>
</template>

<style scoped src="../project-workspace-section.css"></style>
<style scoped src="./testing-workspace.css"></style>
