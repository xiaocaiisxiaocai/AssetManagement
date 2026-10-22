<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import { ElButton, ElDialog } from 'element-plus';

import StartApprovalForm from './StartApprovalForm.vue';

type StartApprovalFormModel = {
  assetId: number | undefined;
  bizType: string;
  reason: string;
  returnDate: string;
  transfereeId: number | undefined;
};

defineProps<{
  assetOptionsLoading: boolean;
  assets: AssetItem[];
  assetSelectCopy: {
    emptyText: string;
    helpText: string;
    placeholder: string;
  };
  disableReturnDate: (time: Date) => boolean;
  inheritedReturnDate: string;
  saving: boolean;
  showReturnDate: boolean;
  transfereeOptions: UserOptionDto[];
  userOptionsLoading: boolean;
}>();

const emit = defineEmits<{
  assetChange: [];
  assetSearch: [keyword: string];
  bizTypeChange: [];
  submit: [];
  userSearch: [keyword: string];
}>();

const visible = defineModel<boolean>('visible', { default: false });
const form = defineModel<StartApprovalFormModel>('form', { required: true });
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="!saving"
    :close-on-press-escape="!saving"
    :show-close="!saving"
    title="发起申请"
    width="540px"
  >
    <StartApprovalForm
      v-model:form="form"
      :asset-options-loading="assetOptionsLoading"
      :asset-select-copy="assetSelectCopy"
      :assets="assets"
      :disable-return-date="disableReturnDate"
      :inherited-return-date="inheritedReturnDate"
      :show-return-date="showReturnDate"
      :transferee-options="transfereeOptions"
      :user-options-loading="userOptionsLoading"
      @asset-change="emit('assetChange')"
      @asset-search="emit('assetSearch', $event)"
      @biz-type-change="emit('bizTypeChange')"
      @user-search="emit('userSearch', $event)"
    />
    <template #footer>
      <ElButton :disabled="saving" @click="visible = false">取消</ElButton>
      <ElButton :disabled="saving" :loading="saving" type="primary" @click="emit('submit')">
        提交
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped src="./mine-dialog.css"></style>
<style scoped src="./StartApprovalDialog.css"></style>
