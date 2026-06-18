<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';
import type { UserDto } from '#/api/user';

import { reactive, ref, watch } from 'vue';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElOption,
  ElSelect,
} from 'element-plus';

import { startApprovalApi } from '#/api/workflow';

const props = defineProps<{ asset: AssetItem | null; users: UserDto[] }>();
const emit = defineEmits<{ submitted: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const saving = ref(false);
const form = reactive({
  reason: '',
  transfereeId: undefined as number | undefined,
});

watch(visible, (opened) => {
  if (opened) {
    form.transfereeId = undefined;
    form.reason = '';
  }
});

async function submit() {
  if (!props.asset) {
    return;
  }
  if (!form.transfereeId) {
    ElMessage.warning('请选择受让人');
    return;
  }
  if (!form.reason || form.reason.length < 10 || form.reason.length > 200) {
    ElMessage.warning('转让原因需要 10-200 字');
    return;
  }
  saving.value = true;
  try {
    await startApprovalApi({
      assetId: props.asset.id,
      bizType: 'transfer',
      reason: form.reason,
      transfereeId: form.transfereeId,
    });
    ElMessage.success('转让申请已提交');
    visible.value = false;
    emit('submitted');
    window.location.href = '/#/approval/mine';
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <ElDialog v-model="visible" title="资产转让申请" width="560px">
    <ElForm v-if="asset" label-width="88px">
      <ElFormItem label="资产编号">
        <ElInput :model-value="asset.assetNo" disabled />
      </ElFormItem>
      <ElFormItem label="资产名称">
        <ElInput :model-value="asset.name" disabled />
      </ElFormItem>
      <ElFormItem label="受让人">
        <ElSelect
          v-model="form.transfereeId"
          filterable
          placeholder="选择受让人"
          style="width: 100%"
        >
          <ElOption
            v-for="item in users"
            :key="item.id"
            :label="`${item.name}(${item.employeeNo})`"
            :value="item.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="转让原因">
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
