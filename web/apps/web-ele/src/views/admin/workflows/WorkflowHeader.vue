<script lang="ts" setup>
import { ElButton } from 'element-plus';

import { buildWorkflowActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildWorkflowActionAccess>;
}>();

const emit = defineEmits<{
  create: [];
}>();
</script>

<template>
  <div class="page-header">
    <div>
      <h2 class="page-title">工作流设计器</h2>
    </div>
    <div class="page-actions">
      <span class="column-tools-host" data-column-tools="admin-workflows"></span>
      <ElButton v-if="access.canCreate" type="primary" @click="emit('create')">
        新增工作流
      </ElButton>
    </div>
  </div>
</template>
