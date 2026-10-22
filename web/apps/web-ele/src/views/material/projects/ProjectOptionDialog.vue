<script lang="ts" setup>
import type { OptionKind } from './project-workspace-types';

import type {
  SaveTestProjectOptionPayload,
  TestProjectOption,
} from '#/api/test-project';
import type { TableColumnOption } from '#/components/table/column-visibility';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTabPane,
  ElTabs,
  ElTag,
} from 'element-plus';

import { useTableColumns } from '#/components/table/column-visibility';
import TableColumnPicker from '#/components/table/TableColumnPicker.vue';

defineProps<{
  canManage: boolean;
  displayedOptions: TestProjectOption[];
  editingId: null | number;
  saving: boolean;
}>();

const emit = defineEmits<{
  edit: [option: TestProjectOption];
  remove: [option: TestProjectOption];
  reset: [kind: OptionKind];
  save: [];
}>();
const form = defineModel<SaveTestProjectOptionPayload>('form', {
  required: true,
});
const visible = defineModel<boolean>('visible', { default: false });
const activeKind = defineModel<OptionKind>('activeKind', { required: true });

const kindLabels: Record<OptionKind, string> = {
  project_progress: '项目进度',
  project_type: '项目类型',
};

function onKindChange(kind: number | string) {
  if (kind === 'project_type' || kind === 'project_progress') {
    // 父组件的 v-model 尚未回传，必须使用本次切换事件的目标分组。
    emit('reset', kind);
  }
}

const tableColumns: TableColumnOption[] = [
  { key: 'kind', label: '类型' },
  { key: 'label', label: '名称' },
  { key: 'sort', label: '排序' },
  { key: 'active', label: '启用' },
  { key: 'actions', label: '操作', locked: true },
];
const { hiddenKeys, resetColumns, setColumnVisible, visibleColumn } =
  useTableColumns('project-options', tableColumns);
</script>

<template>
  <ElDialog v-model="visible" title="项目配置" width="760px">
    <ElTabs v-model="activeKind" @tab-change="onKindChange">
      <ElTabPane label="项目类型" name="project_type" />
      <ElTabPane label="项目进度" name="project_progress" />
    </ElTabs>
    <div class="option-editor">
      <div class="option-form-row">
        <ElForm class="option-form" label-width="52px">
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
          <TableColumnPicker
            :columns="tableColumns"
            :hidden-keys="hiddenKeys"
            @reset="resetColumns"
            @toggle="setColumnVisible"
          />
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
    </div>
    <ElTable :data="displayedOptions" border>
      <ElTableColumn v-if="visibleColumn('kind')" label="类型" width="110">
        <template #default="{ row }">
          {{ kindLabels[row.kind as OptionKind] }}
        </template>
      </ElTableColumn>
      <ElTableColumn v-if="visibleColumn('label')" label="名称" prop="label" />
      <ElTableColumn
        v-if="visibleColumn('sort')"
        align="center"
        label="排序"
        prop="sort"
        width="80"
      />
      <ElTableColumn
        v-if="visibleColumn('active')"
        align="center"
        label="启用"
        width="80"
      >
        <template #default="{ row }">
          <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn
        v-if="visibleColumn('actions')"
        align="center"
        class-name="table-action-cell"
        label="操作"
        width="130"
      >
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

<style scoped src="./ProjectOptionDialog.css"></style>
