<script lang="ts" setup>
import type { MaterialItem, TransfereeOption } from '#/api/material';

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

import { initiateTransferApi, listTransfereeOptionsApi } from '#/api/material';

const props = defineProps<{
  material: MaterialItem | null;
}>();
const emit = defineEmits<{ done: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const saving = ref(false);
const optionsLoading = ref(false);
const transfereeOptions = ref<TransfereeOption[]>([]);
let submitSequence = 0;
let searchSequence = 0;

const form = reactive({
  reason: '',
  transfereeId: undefined as number | undefined,
});

async function searchTransferees(keyword: string) {
  const materialId = props.material?.id;
  if (!materialId) return;
  const sequence = ++searchSequence;
  optionsLoading.value = true;
  try {
    const items = await listTransfereeOptionsApi(materialId, keyword);
    if (sequence !== searchSequence || props.material?.id !== materialId) {
      return;
    }
    transfereeOptions.value = items;
  } catch {
    if (sequence === searchSequence) transfereeOptions.value = [];
  } finally {
    if (sequence === searchSequence) optionsLoading.value = false;
  }
}

watch(visible, (opened) => {
  if (opened) {
    submitSequence++;
    searchSequence++;
    form.transfereeId = undefined;
    form.reason = '';
    transfereeOptions.value = [];
    void searchTransferees('');
  }
});

async function submit() {
  if (saving.value) return;
  if (!props.material) {
    return;
  }
  if (!form.transfereeId) {
    ElMessage.warning('请选择受让人');
    return;
  }
  if (
    props.material.custodianId &&
    form.transfereeId === props.material.custodianId
  ) {
    ElMessage.warning('受让人不能是当前保管人');
    return;
  }
  const materialId = props.material.id;
  const requestSequence = ++submitSequence;
  saving.value = true;
  try {
    const flow = await initiateTransferApi({
      materialId: props.material.id,
      reason: form.reason || undefined,
      transfereeId: form.transfereeId,
    });
    ElMessage.success(
      flow.directTransfer ? '已直接转移给受让人' : '已提交审批,等待审批',
    );
    if (
      requestSequence === submitSequence &&
      props.material?.id === materialId
    ) {
      visible.value = false;
      emit('done');
    }
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <ElDialog
    v-model="visible"
    :close-on-click-modal="!saving"
    :close-on-press-escape="!saving"
    :show-close="!saving"
    title="测试料件流转"
    width="520px"
  >
    <ElForm v-if="material" label-width="88px">
      <ElFormItem label="料件编号">
        <ElInput :model-value="material.materialNo" disabled />
      </ElFormItem>
      <ElFormItem label="料件名称">
        <ElInput :model-value="material.name" disabled />
      </ElFormItem>
      <ElFormItem label="当前保管人">
        <ElInput :model-value="material.custodianName ?? '未指定'" disabled />
      </ElFormItem>
      <ElFormItem label="受让人">
        <ElSelect
          v-model="form.transfereeId"
          :loading="optionsLoading"
          :remote-method="searchTransferees"
          filterable
          placeholder="选择受让人"
          remote
          style="width: 100%"
        >
          <ElOption
            v-for="item in transfereeOptions"
            :key="item.id"
            :label="`${item.name}(${item.employeeNo})`"
            :value="item.id"
          />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="转移原因">
        <ElInput
          v-model="form.reason"
          :maxlength="500"
          :rows="3"
          placeholder="可选"
          show-word-limit
          type="textarea"
        />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton :disabled="saving" @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="submit">提交</ElButton>
    </template>
  </ElDialog>
</template>
