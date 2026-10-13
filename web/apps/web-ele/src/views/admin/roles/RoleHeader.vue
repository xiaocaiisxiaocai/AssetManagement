<script lang="ts" setup>
import { ElButton } from 'element-plus';

import { buildRoleActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildRoleActionAccess>;
}>();

const emit = defineEmits<{
  create: [];
}>();
</script>

<template>
  <div class="role-header">
    <div>
      <h2 class="role-title">角色管理</h2>
    </div>
    <ElButton v-if="access.canCreate" type="primary" @click="emit('create')">
      新增角色
    </ElButton>
  </div>
</template>

<style scoped src="./RoleHeader.css"></style>
