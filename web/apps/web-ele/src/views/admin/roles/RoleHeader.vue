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
    <div class="page-actions">
      <span class="column-tools-host" data-column-tools="admin-roles"></span>
      <ElButton v-if="access.canCreate" type="primary" @click="emit('create')">
        新增角色
      </ElButton>
    </div>
  </div>
</template>

<style scoped src="./RoleHeader.css"></style>
