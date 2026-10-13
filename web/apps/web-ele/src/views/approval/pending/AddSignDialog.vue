<script lang="ts" setup>
import type { UserOptionDto } from '#/api/user';

import { ElButton, ElDialog, ElOption, ElSelect } from 'element-plus';

defineProps<{
  candidates: UserOptionDto[];
  loading: boolean;
}>();

const emit = defineEmits<{
  confirm: [];
}>();

const visible = defineModel<boolean>('visible', { default: false });
const addSignUser = defineModel<string>('addSignUser', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="false"
    title="加签"
    width="460px"
  >
    <ElSelect
      v-model="addSignUser"
      filterable
      placeholder="选择部门主管"
      style="width: 100%"
    >
      <ElOption
        v-for="user in candidates"
        :key="user.id"
        :label="`${user.name}（${user.employeeNo}）`"
        :value="String(user.id)"
      />
    </ElSelect>

    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton
        :disabled="loading"
        :loading="loading"
        type="primary"
        @click="emit('confirm')"
      >
        确认加签
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./pending-approval-dialog.css"></style>
