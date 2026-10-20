<script lang="ts" setup>
import type {
  ProjectStageDto,
  ProjectStageStatus,
} from '#/api/project-workspace';

import { computed } from 'vue';

import {
  ElButton,
  ElEmpty,
  ElResult,
  ElSkeleton,
  ElSkeletonItem,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { dateOnly } from './project-testing-rules';
import ProjectReportDocuments from './ProjectReportDocuments.vue';
import ProjectStageDialog from './ProjectStageDialog.vue';
import { useProjectTestingWorkspace } from './use-project-testing-workspace';

defineOptions({ name: 'ProjectTestingWorkspace' });

const props = defineProps<{
  enabled: boolean;
  projectId: number;
}>();

const {
  canExport,
  canManage,
  deletingDocumentId,
  deletingStageId,
  downloadDocument,
  downloadingKey,
  downloadPackage,
  downloadReport,
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
</script>

<template>
  <div v-if="enabled" class="testing-workspace">
    <div v-if="loading && !workspace" class="testing-loading-panel">
      <ElSkeleton :rows="7" animated>
        <template #template>
          <div class="testing-skeleton-header">
            <ElSkeletonItem style="width: 32%; height: 38px" variant="rect" />
            <ElSkeletonItem style="width: 28%; height: 38px" variant="rect" />
          </div>
          <ElSkeletonItem style="width: 100%; height: 280px" variant="rect" />
        </template>
      </ElSkeleton>
    </div>

    <ElResult
      v-else-if="loadError"
      :sub-title="loadError"
      icon="error"
      title="测试阶段与报告加载失败"
    >
      <template #extra>
        <ElButton :loading="loading" type="primary" @click="loadWorkspace">
          重新加载
        </ElButton>
      </template>
    </ElResult>

    <template v-else-if="workspace">
      <section class="testing-summary-bar">
        <div class="testing-summary-item testing-summary-current">
          <span>当前阶段</span>
          <strong>
            {{
              currentStage
                ? `${currentStage.sequence}. ${currentStage.name}`
                : '暂无进行中阶段'
            }}
          </strong>
          <ElTag
            v-if="currentStage"
            :type="statusMap[currentStage.status].type"
            size="small"
          >
            {{ statusMap[currentStage.status].label }}
          </ElTag>
        </div>
        <div class="testing-summary-item">
          <span>阶段进度</span>
          <strong>{{ completedCount }} / {{ orderedStages.length }}</strong>
          <small>已完成 / 全部</small>
        </div>
        <div class="testing-summary-actions">
          <ElTag v-if="!canManage" size="small" type="info">只读</ElTag>
          <ElButton
            v-if="canExport"
            :disabled="
              Boolean(downloadingKey) && downloadingKey !== 'report-all'
            "
            :loading="downloadingKey === 'report-all'"
            @click="downloadReport()"
          >
            导出项目报告
          </ElButton>
          <ElButton
            v-if="canExport"
            :disabled="
              Boolean(downloadingKey) && downloadingKey !== 'package-all'
            "
            :loading="downloadingKey === 'package-all'"
            @click="downloadPackage()"
          >
            导出完整资料包
          </ElButton>
          <ElButton
            v-if="canManage"
            :disabled="writeBusy"
            type="primary"
            @click="openCreateStage"
          >
            新增阶段
          </ElButton>
        </div>
      </section>

      <section class="testing-section stage-ledger-section">
        <header class="testing-section-header">
          <div>
            <h3>测试阶段台账</h3>
            <p>
              记录阶段目标、测试过程和测试结果；结案阶段报告作为项目总报告。
            </p>
          </div>
        </header>
        <div class="stage-table-wrap" v-loading="loading">
          <ElTable
            v-if="orderedStages.length > 0"
            :data="orderedStages"
            :max-height="480"
            border
            class="compact-table stage-ledger-table"
            empty-text="暂无测试阶段"
            row-key="id"
            table-layout="fixed"
          >
            <ElTableColumn align="center" label="阶段序号" width="88">
              <template #default="{ row }">{{ row.sequence }}</template>
            </ElTableColumn>
            <ElTableColumn align="center" label="阶段名称" min-width="150">
              <template #default="{ row }">
                <span :title="row.name" class="stage-name-cell">{{
                  row.name
                }}</span>
              </template>
            </ElTableColumn>
            <ElTableColumn align="center" label="目标" min-width="180">
              <template #default="{ row }">
                <span :title="row.objective || ''" class="multiline-preview">
                  {{ displayText(row.objective) }}
                </span>
              </template>
            </ElTableColumn>
            <ElTableColumn align="center" label="测试内容" min-width="220">
              <template #default="{ row }">
                <span :title="row.testContent || ''" class="multiline-preview">
                  {{ displayText(row.testContent) }}
                </span>
              </template>
            </ElTableColumn>
            <ElTableColumn align="center" label="测试结果" min-width="240">
              <template #default="{ row }">
                <span :title="row.testResult || ''" class="multiline-preview">
                  {{ displayText(row.testResult) }}
                </span>
              </template>
            </ElTableColumn>
            <ElTableColumn align="center" label="起止日期" width="190">
              <template #default="{ row }">{{ dateRange(row) }}</template>
            </ElTableColumn>
            <ElTableColumn align="center" label="状态" width="96">
              <template #default="{ row }">
                <ElTag :type="statusMeta(row.status).type" size="small">
                  {{ statusMeta(row.status).label }}
                </ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn
              align="center"
              fixed="right"
              label="报告附件"
              width="210"
            >
              <template #default="{ row }">
                <ProjectReportDocuments
                  :key="row.id"
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
              v-if="canManage || canExport"
              :width="canManage ? 220 : 150"
              align="center"
              class-name="table-action-cell"
              fixed="right"
              label="操作"
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
                  <ElButton
                    v-if="canExport"
                    :disabled="
                      Boolean(downloadingKey) &&
                      downloadingKey !== `report-${row.id}`
                    "
                    :loading="downloadingKey === `report-${row.id}`"
                    link
                    size="small"
                    type="primary"
                    @click="downloadReport(row)"
                  >
                    导出报告
                  </ElButton>
                  <ElButton
                    v-if="canExport"
                    :disabled="
                      Boolean(downloadingKey) &&
                      downloadingKey !== `package-${row.id}`
                    "
                    :loading="downloadingKey === `package-${row.id}`"
                    link
                    size="small"
                    type="primary"
                    @click="downloadPackage(row)"
                  >
                    资料包
                  </ElButton>
                </div>
              </template>
            </ElTableColumn>
          </ElTable>
          <ElEmpty
            v-else
            :description="
              canManage ? '暂无测试阶段，可新增第一阶段' : '暂无测试阶段'
            "
          />
        </div>
      </section>
    </template>

    <ProjectStageDialog
      v-model:form="stageForm"
      v-model:visible="stageDialogVisible"
      :editing="Boolean(editingStage)"
      :saving="savingStage"
      @save="saveStage"
    />
  </div>
</template>

<style scoped src="./testing-workspace.css"></style>
