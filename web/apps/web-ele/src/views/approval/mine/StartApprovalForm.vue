<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import {
  ElDatePicker,
  ElForm,
  ElFormItem,
  ElInput,
  ElOption,
  ElSelect,
} from 'element-plus';

import { isApprovalReasonRequired } from './approval-form-rules';

type StartApprovalFormState = {
  assetId?: number;
  bizType: string;
  reason: string;
  returnDate: string;
  transfereeId?: number;
};

defineProps<{
  assetOptionsLoading: boolean;
  assets: AssetItem[];
  assetSelectCopy: { emptyText: string; helpText: string; placeholder: string };
  disableReturnDate: (time: Date) => boolean;
  inheritedReturnDate: string;
  showReturnDate: boolean;
  transfereeOptions: UserOptionDto[];
  userOptionsLoading: boolean;
}>();

const emit = defineEmits<{
  assetChange: [];
  assetSearch: [keyword: string];
  bizTypeChange: [];
  userSearch: [keyword: string];
}>();

const form = defineModel<StartApprovalFormState>('form', { required: true });
</script>

<template>
  <ElForm class="start-approval-form" label-width="100px">
    <ElFormItem label="申请类型" required>
      <ElSelect
        v-model="form.bizType"
        style="width: 100%"
        @change="emit('bizTypeChange')"
      >
        <ElOption label="借用" value="borrow" />
        <ElOption label="转让" value="transfer" />
        <ElOption label="延期" value="extension" />
        <ElOption label="归还" value="return" />
      </ElSelect>
    </ElFormItem>
    <ElFormItem label="资产" required>
      <ElSelect
        v-model="form.assetId"
        :filter-method="(keyword: string) => emit('assetSearch', keyword)"
        :loading="assetOptionsLoading"
        :placeholder="assetSelectCopy.placeholder"
        clearable
        default-first-option
        filterable
        style="width: 100%"
        @change="emit('assetChange')"
      >
        <ElOption
          v-for="asset in assets"
          :key="asset.id"
          :label="`${asset.assetNo} / ${asset.name}`"
          :value="asset.id"
        />
        <template #empty>
          <div class="asset-select-empty">
            {{ assetSelectCopy.emptyText }}
          </div>
        </template>
      </ElSelect>
      <div class="asset-select-help">
        {{ assetSelectCopy.helpText }}
      </div>
    </ElFormItem>
    <ElFormItem
      v-if="form.bizType === 'extension' && form.assetId"
      label="原应归还日期"
    >
      <ElInput :model-value="inheritedReturnDate" disabled />
      <div class="asset-select-help">
        延期审批通过前仍按原日期计算到期和逾期状态
      </div>
    </ElFormItem>
    <ElFormItem
      v-if="showReturnDate"
      :label="form.bizType === 'extension' ? '新归还日期' : '归还日期'"
      required
    >
      <ElDatePicker
        v-model="form.returnDate"
        :disabled-date="disableReturnDate"
        :placeholder="
          form.bizType === 'extension' ? '选择新的归还日期' : '选择归还日期'
        "
        style="width: 100%"
        type="date"
        value-format="YYYY-MM-DD"
      />
    </ElFormItem>
    <ElFormItem v-if="form.bizType === 'transfer'" label="接收人" required>
      <ElSelect
        v-model="form.transfereeId"
        :loading="userOptionsLoading"
        :remote-method="(keyword: string) => emit('userSearch', keyword)"
        filterable
        placeholder="选择接收人"
        remote
        style="width: 100%"
      >
        <ElOption
          v-for="user in transfereeOptions"
          :key="user.id"
          :label="`${user.name}（${user.employeeNo}）`"
          :value="user.id"
        />
      </ElSelect>
    </ElFormItem>
    <ElFormItem
      v-if="form.bizType === 'transfer' && form.assetId"
      label="原应归还日期"
    >
      <ElInput :model-value="inheritedReturnDate" disabled />
      <div class="asset-select-help">
        转让只变更当前保管人，不会重新计算原借用期限
      </div>
    </ElFormItem>
    <ElFormItem
      :required="isApprovalReasonRequired(form.bizType)"
      label="申请事由"
    >
      <ElInput
        v-model="form.reason"
        :rows="3"
        placeholder="请输入申请事由"
        type="textarea"
      />
    </ElFormItem>
  </ElForm>
</template>

<style scoped src="./mine-dialog.css"></style>
<style scoped src="./StartApprovalDialog.css"></style>
