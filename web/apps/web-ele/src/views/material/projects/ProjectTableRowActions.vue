<script lang="ts" setup>
import type { TestProjectItem } from '#/api/test-project';

import { ElButton } from 'element-plus';

import { canUpdateProjectProgress } from './project-workspace-rules';

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
  row: TestProjectItem;
}>();

const emit = defineEmits<{
  edit: [];
  progress: [];
  purge: [];
  remove: [];
  restore: [];
}>();
</script>

<template>
  <template v-if="!row.isDeleted">
    <ElButton
      v-if="access.canEdit && !row.closedDate"
      link
      size="small"
      type="primary"
      @click="emit('edit')"
    >
      编辑
    </ElButton>
    <ElButton
      v-else-if="canUpdateProjectProgress(row, currentUserId)"
      link
      size="small"
      type="primary"
      @click="emit('progress')"
    >
      更新进展
    </ElButton>
    <ElButton
      v-if="access.canDelete"
      link
      size="small"
      type="danger"
      @click="emit('remove')"
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
      @click="emit('restore')"
    >
      撤销删除
    </ElButton>
    <ElButton
      v-if="access.canPurge"
      link
      size="small"
      type="danger"
      @click="emit('purge')"
    >
      彻底删除
    </ElButton>
  </template>
</template>
