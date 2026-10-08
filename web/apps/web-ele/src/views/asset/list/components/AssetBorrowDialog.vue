<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';

import { reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

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
import {
  disableNonFutureReturnDate,
  isFutureReturnDate,
} from '#/utils/return-date';

const props = defineProps<{ asset: AssetItem | null }>();

const emit = defineEmits<{ submitted: [] }>();

const router = useRouter();

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
  if (!isFutureReturnDate(form.returnDate as string)) {
    ElMessage.warning('归还日期必须晚于今天');
    return;
  }
  const reason = form.reason.trim();
  if (!reason) {
    ElMessage.warning('请填写借用原因');
    return;
  }
  if (reason.length > 500) {
    ElMessage.warning('借用原因不能超过 500 个字符');
    return;
  }
  saving.value = true;
  try {
    await startApprovalApi({
      assetId: props.asset.id,
      bizType: 'borrow',
      reason,
      returnDate: form.returnDate as string,
    });
    ElMessage.success('借用申请已提交');
    visible.value = false;
    emit('submitted');
    router.push('/approval/mine');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
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
      <ElFormItem label="归还日期" required>
        <ElDatePicker
          v-model="form.returnDate"
          :disabled-date="disableNonFutureReturnDate"
          clearable
          format="YYYY-MM-DD"
          placeholder="选择归还日期（必填）"
          style="width: 100%"
          type="date"
          value-format="YYYY-MM-DD"
        />
      </ElFormItem>
      <ElFormItem label="借用原因" required>
        <ElInput
          v-model="form.reason"
          :maxlength="500"
          :rows="3"
          clearable
          placeholder="请填写借用原因，最多 500 字"
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
