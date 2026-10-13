<script lang="ts" setup>
import { ElButton } from 'element-plus';

import { buildUserActionAccess } from '#/views/permissions/action-access';

defineProps<{
  access: ReturnType<typeof buildUserActionAccess>;
}>();

const emit = defineEmits<{
  create: [];
  import: [];
}>();
</script>

<template>
  <div class="user-header">
    <div>
      <h2 class="user-title">用户管理</h2>
    </div>
    <div class="user-header-actions">
      <ElButton v-if="access.canImport" @click="emit('import')">
        批量导入
      </ElButton>
      <ElButton v-if="access.canCreate" type="primary" @click="emit('create')">
        新增用户
      </ElButton>
    </div>
  </div>
</template>

<style scoped src="./UserHeader.css"></style>
