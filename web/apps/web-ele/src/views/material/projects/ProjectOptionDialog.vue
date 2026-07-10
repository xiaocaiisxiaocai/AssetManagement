<script lang="ts" setup>
import type {
  SaveTestProjectOptionPayload,
  TestProjectOption,
} from '#/api/test-project';
import type { OptionKind } from './project-workspace-types';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElSwitch,
  ElTabPane,
  ElTable,
  ElTableColumn,
  ElTabs,
  ElTag,
} from 'element-plus';

defineProps<{
  canManage: boolean;
  displayedOptions: TestProjectOption[];
  editingId: null | number;
  form: SaveTestProjectOptionPayload;
  saving: boolean;
}>();

const emit = defineEmits<{
  edit: [option: TestProjectOption];
  remove: [option: TestProjectOption];
  reset: [kind: OptionKind];
  save: [];
}>();
const visible = defineModel<boolean>('visible', { default: false });
const activeKind = defineModel<OptionKind>('activeKind', { required: true });

const kindLabels: Record<OptionKind, string> = {
  project_progress: '项目进度',
  project_type: '项目类型',
};
</script>

<template>
  <ElDialog v-model="visible" title="项目配置" width="760px">
    <ElTabs v-model="activeKind" @tab-change="emit('reset', activeKind)">
      <ElTabPane label="项目类型" name="project_type" />
      <ElTabPane label="项目进度" name="project_progress" />
    </ElTabs>
    <div class="option-editor">
      <ElForm label-width="70px">
        <div class="option-form-grid">
          <ElFormItem label="名称">
            <ElInput v-model="form.label" placeholder="中文名称" />
          </ElFormItem>
          <ElFormItem label="排序">
            <ElInputNumber
              v-model="form.sort"
              controls-position="right"
              style="width: 100%"
            />
          </ElFormItem>
          <ElFormItem label="启用">
            <ElSwitch v-model="form.isActive" />
          </ElFormItem>
        </div>
      </ElForm>
      <div class="option-actions">
        <ElButton
          v-if="canManage && editingId"
          @click="emit('reset', activeKind)"
        >
          取消编辑
        </ElButton>
        <ElButton
          v-if="canManage"
          :loading="saving"
          type="primary"
          @click="emit('save')"
        >
          {{ editingId ? '保存修改' : '新增' }}
        </ElButton>
      </div>
    </div>
    <ElTable :data="displayedOptions" border>
      <ElTableColumn label="类型" width="110">
        <template #default="{ row }">{{
          kindLabels[row.kind as OptionKind]
        }}</template>
      </ElTableColumn>
      <ElTableColumn label="名称" prop="label" />
      <ElTableColumn align="center" label="排序" prop="sort" width="80" />
      <ElTableColumn align="center" label="启用" width="80">
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="操作" width="130">
        <template #default="{ row }">
          <ElButton
            v-if="canManage"
            link
            size="small"
            type="primary"
            @click="emit('edit', row)"
          >
            编辑
          </ElButton>
          <ElButton
            v-if="canManage"
            link
            size="small"
            type="danger"
            @click="emit('remove', row)"
          >
            删除
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>
  </ElDialog>
</template>

<style scoped>
.option-editor {
  padding: 12px;
  margin-bottom: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.option-form-grid {
  display: grid;
  grid-template-columns: 1.4fr 0.8fr 0.6fr;
  column-gap: 12px;
}

.option-actions {
  display: flex;
  flex-shrink: 0;
  justify-content: flex-end;
  gap: 8px;
}

@media (max-width: 768px) {
  .option-form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
