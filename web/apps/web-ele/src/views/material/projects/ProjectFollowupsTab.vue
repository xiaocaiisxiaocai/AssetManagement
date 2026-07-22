<script lang="ts" setup>
import type { TestProjectFollowup, TestProjectItem } from '#/api/test-project';

import { computed } from 'vue';

import {
  ElButton,
  ElDatePicker,
  ElEmpty,
  ElForm,
  ElFormItem,
  ElInput,
  ElTabPane,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

import { formatDate, formatDateTime } from '#/utils/date-format';

import { isFutureFollowupDate } from './followup-date-rules';

const props = defineProps<{
  editingId: null | number;
  followups: TestProjectFollowup[];
  loading: boolean;
  project: TestProjectItem;
  saving: boolean;
}>();
const emit = defineEmits<{
  cancelEdit: [];
  edit: [followup: TestProjectFollowup];
  remove: [followup: TestProjectFollowup];
  save: [];
}>();
const form = defineModel<{ content: string; dueDate: string }>('form', {
  required: true,
});
const canWrite = computed(
  () => props.project.canWriteFollowUp && !props.project.isDeleted,
);
const disabledFollowupDate = (date: Date) => isFutureFollowupDate(date);
</script>

<template>
  <ElTabPane name="followups">
    <template #label>
      <span>落地跟进</span><span class="tab-count">{{ followups.length }}</span>
    </template>
    <div class="followup-workspace">
      <aside class="followup-editor-panel">
        <div class="panel-heading">
          <div>
            <h3>{{ editingId ? '编辑跟进记录' : '新增跟进记录' }}</h3>
            <p>记录本周期进展、问题和下一步动作。</p>
          </div>
          <ElTag v-if="!canWrite" type="info">只读</ElTag>
        </div>
        <template v-if="canWrite">
          <ElForm label-position="top">
            <ElFormItem label="跟进日期">
              <ElDatePicker
                v-model="form.dueDate"
                :disabled-date="disabledFollowupDate"
                placeholder="选择对应周期日期"
                style="width: 100%"
                type="date"
                value-format="YYYY-MM-DD"
              />
            </ElFormItem>
            <ElFormItem label="落地情况">
              <ElInput
                v-model="form.content"
                :rows="7"
                class="followup-textarea"
                maxlength="2000"
                placeholder="填写本周期落地进展、问题和下一步"
                resize="none"
                show-word-limit
                type="textarea"
              />
            </ElFormItem>
          </ElForm>
          <div class="followup-actions">
            <ElButton v-if="editingId" @click="emit('cancelEdit')">
              取消编辑
            </ElButton>
            <ElButton :loading="saving" type="primary" @click="emit('save')">
              {{ editingId ? '保存修改' : '新增跟进' }}
            </ElButton>
          </div>
        </template>
        <div v-else class="readonly-note">
          {{
            project.isDeleted
              ? '项目已删除，仅保留历史记录供查看。'
              : project.closedDate
                ? '项目已结案，跟进记录已锁定。'
                : '项目进入落地跟进后，负责人或管理员才能填写。'
          }}
        </div>
      </aside>
      <section class="followup-history-panel" v-loading="loading">
        <div class="panel-heading">
          <div>
            <h3>历史跟进</h3>
            <p>按填写时间倒序展示。</p>
          </div>
        </div>
        <ElEmpty
          v-if="followups.length === 0 && !loading"
          class="compact-empty"
          description="暂无跟进记录"
        />
        <ElTimeline v-else class="followup-timeline">
          <ElTimelineItem
            v-for="item in followups"
            :key="item.id"
            :timestamp="formatDate(item.dueDate)"
            placement="top"
          >
            <article class="followup-record">
              <div class="followup-record-meta">
                <span>{{ item.filledByName || '-' }}</span>
                <span>{{ formatDateTime(item.filledAt) }}</span>
              </div>
              <div class="followup-content">{{ item.content }}</div>
              <div v-if="canWrite" class="record-actions">
                <ElButton
                  link
                  size="small"
                  type="primary"
                  @click="emit('edit', item)"
                >
                  编辑
                </ElButton>
                <ElButton
                  link
                  size="small"
                  type="danger"
                  @click="emit('remove', item)"
                >
                  删除
                </ElButton>
              </div>
            </article>
          </ElTimelineItem>
        </ElTimeline>
      </section>
    </div>
  </ElTabPane>
</template>

<style scoped>
.tab-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 18px;
  padding: 0 6px;
  margin-left: 6px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 18px;
  background: var(--el-fill-color-light);
  border-radius: 999px;
}
.followup-workspace {
  display: grid;
  grid-template-columns: minmax(320px, 0.48fr) minmax(0, 1fr);
  gap: 14px;
  height: 100%;
  min-height: 0;
  align-items: stretch;
}
.followup-editor-panel,
.followup-history-panel {
  min-width: 0;
  padding: 18px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 10px;
  background: var(--el-fill-color-blank);
}
.followup-editor-panel {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  position: sticky;
  top: 0;
  border-top: 3px solid var(--el-color-primary);
  background: color-mix(
    in srgb,
    var(--el-color-primary) 4%,
    var(--el-fill-color-blank)
  );
}
.followup-editor-panel :deep(.el-form) {
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
}
.followup-editor-panel :deep(.el-form-item:last-child) {
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
}
.followup-editor-panel :deep(.el-form-item:last-child .el-form-item__content) {
  min-height: 0;
  flex: 1;
}
.followup-textarea {
  height: 100%;
}
.followup-textarea :deep(.el-textarea__inner) {
  height: 100%;
  max-height: 100%;
  resize: none;
}
.followup-history-panel {
  min-height: 0;
  overflow: auto;
  background: var(--el-fill-color-blank);
  box-shadow: inset 3px 0 0
    color-mix(in srgb, var(--el-color-primary) 14%, transparent);
}
.followup-actions,
.record-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
.followup-actions {
  flex-shrink: 0;
  margin-top: 12px;
}
.panel-heading {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: flex-start;
  margin-bottom: 14px;
}
.panel-heading h3 {
  margin: 0;
  color: var(--el-text-color-primary);
  font-size: 15px;
  font-weight: 700;
}
.panel-heading p {
  margin: 4px 0 0;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 1.5;
}
.readonly-note {
  padding: 14px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  background: var(--el-fill-color-light);
  border-radius: 6px;
}
.compact-empty {
  padding: 54px 0;
}
.followup-timeline {
  padding: 2px 6px 0 2px;
}

.followup-timeline :deep(.el-timeline-item__node) {
  background: var(--el-color-primary);
  box-shadow: 0 0 0 4px
    color-mix(in srgb, var(--el-color-primary) 12%, transparent);
}
.followup-record {
  padding: 13px 15px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: color-mix(
    in srgb,
    var(--el-fill-color-light) 72%,
    var(--el-fill-color-blank)
  );
  transition:
    border-color 0.16s ease,
    box-shadow 0.16s ease;
}

.followup-record:hover {
  border-color: color-mix(
    in srgb,
    var(--el-color-primary) 35%,
    var(--el-border-color-lighter)
  );
  box-shadow: 0 5px 16px rgb(31 78 121 / 7%);
}
.followup-record-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 12px;
  margin-bottom: 8px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.followup-content {
  color: var(--el-text-color-primary);
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
}
.record-actions {
  margin-top: 8px;
}
@media (max-width: 768px) {
  .followup-workspace {
    grid-template-columns: 1fr;
  }
  .followup-editor-panel {
    position: static;
  }
}
</style>
