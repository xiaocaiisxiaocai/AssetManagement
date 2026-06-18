<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';

import { reactive, ref, watch } from 'vue';

import {
  ElButton,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
} from 'element-plus';

import { startApprovalApi } from '#/api/workflow';

const props = defineProps<{ asset: AssetItem | null }>();
const emit = defineEmits<{ submitted: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const saving = ref(false);
const form = reactive({ reason: '', returnDate: '' as Date | string });

watch(visible, (opened) => {
  if (opened) {
    form.returnDate = '';
    form.reason = '';
  }
});

async function submit() {
  if (!props.asset) {
    return;
  }
  if (!form.returnDate) {
    ElMessage.warning('请选择归还日期');
    return;
  }
  if (!form.reason || form.reason.length < 10 || form.reason.length > 200) {
    ElMessage.warning('借用原因需要 10-200 字');
    return;
  }
  saving.value = true;
  try {
    await startApprovalApi({
      assetId: props.asset.id,
      bizType: 'borrow',
      reason: form.reason,
      returnDate: form.returnDate as string,
    });
    ElMessage.success('借用申请已提交');
    visible.value = false;
    emit('submitted');
    window.location.href = '/#/approval/mine';
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <ElDialog v-model="visible" title="资产借用申请" width="560px">
    <ElForm v-if="asset" label-width="88px">
      <ElFormItem label="资产编号">
        <ElInput :model-value="asset.assetNo" disabled />
      </ElFormItem>
      <ElFormItem label="资产名称">
        <ElInput :model-value="asset.name" disabled />
      </ElFormItem>
      <ElFormItem label="归还日期">
        <ElDatePicker
          v-model="form.returnDate"
          clearable
          format="YYYY-MM-DD"
          placeholder="选择归还日期"
          style="width: 100%"
          type="date"
        />
      </ElFormItem>
      <ElFormItem label="借用原因">
        <ElInput
          v-model="form.reason"
          :maxlength="200"
          :rows="3"
          clearable
          placeholder="10-200 字"
          show-word-limit
          type="textarea"
        />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="submit">提交</ElButton>
    </template>
  </ElDialog>
</template>
