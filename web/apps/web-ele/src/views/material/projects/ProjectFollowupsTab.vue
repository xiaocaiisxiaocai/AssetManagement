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
const latestFollowupDate = computed(() => {
  const dates = props.followups
    .map((item) => item.dueDate?.slice(0, 10))
    .filter((date): date is string => Boolean(date));
  return dates.reduce<string | null>(
    (latest, current) => (!latest || current > latest ? current : latest),
    null,
  );
});
const disabledFollowupDate = (date: Date) => {
  if (isFutureFollowupDate(date)) return true;
  if (props.editingId || !latestFollowupDate.value) return false;

  const candidate = new Date(
    date.getFullYear(),
    date.getMonth(),
    date.getDate(),
  );
  const earliest = new Date(`${latestFollowupDate.value}T00:00:00`);
  return candidate.getTime() < earliest.getTime();
};
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
                :disabled="Boolean(editingId)"
                placeholder="选择对应周期日期"
                style="width: 100%"
                type="date"
                value-format="YYYY-MM-DD"
              />
              <p v-if="editingId" class="followup-date-hint">
                历史跟进日期不可修改
              </p>
              <p v-else-if="latestFollowupDate" class="followup-date-hint">
                已有跟进至
                {{ formatDate(latestFollowupDate) }}，同日或之后可新增
              </p>
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
            placement="top"
          >
            <div class="followup-record-heading">
              <div class="followup-record-meta">
                <span>{{ item.filledByName || '-' }}</span>
                <span>{{ formatDateTime(item.filledAt) }}</span>
              </div>
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
            </div>
            <article class="followup-record">
              <div class="followup-content">{{ item.content }}</div>
            </article>
          </ElTimelineItem>
        </ElTimeline>
      </section>
    </div>
  </ElTabPane>
</template>

<style scoped src="./ProjectFollowupsTab.css"></style>
