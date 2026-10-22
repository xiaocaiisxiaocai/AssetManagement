<script lang="ts" setup>
import type { TestProjectItem } from '#/api/test-project';

import { ElButton } from 'element-plus';

defineProps<{
  row: TestProjectItem;
}>();

const emit = defineEmits<{
  edit: [];
  progress: [];
  purge: [];
  remove: [];
  reopen: [];
  restore: [];
}>();
</script>

<template>
  <template v-if="!row.isDeleted">
    <ElButton
      v-if="row.canEdit"
      link
      size="small"
      type="primary"
      @click="emit('edit')"
    >
      编辑
    </ElButton>
    <ElButton
      v-else-if="row.canUpdateProgress"
      link
      size="small"
      type="primary"
      @click="emit('progress')"
    >
      更新进展
    </ElButton>
    <ElButton
      v-if="row.canReopen"
      link
      size="small"
      type="warning"
      @click="emit('reopen')"
    >
      重新打开
    </ElButton>
    <ElButton
      v-if="row.canDelete"
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
      v-if="row.canRestore"
      link
      size="small"
      type="success"
      @click="emit('restore')"
    >
      撤销删除
    </ElButton>
    <ElButton
      v-if="row.canPurge"
      link
      size="small"
      type="danger"
      @click="emit('purge')"
    >
      彻底删除
    </ElButton>
  </template>
</template>
