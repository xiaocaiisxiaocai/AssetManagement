<script lang="ts" setup>
import { ElButton } from 'element-plus';

import { buildDepartmentActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildDepartmentActionAccess>;
}>();

const emit = defineEmits<{
  create: [];
}>();
</script>

<template>
  <div class="page-header">
    <div>
      <h2 class="page-title">组织架构管理</h2>
    </div>
    <ElButton v-if="access.canCreate" type="primary" @click="emit('create')">
      新增组织架构
    </ElButton>
  </div>
</template>
