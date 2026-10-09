<script lang="ts" setup>
import type { MaterialItem } from '#/api/material';
import type { UserOptionDto } from '#/api/user';

import { computed, reactive, ref, watch } from 'vue';

import { useUserStore } from '@vben/stores';

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

import { initiateTransferApi } from '#/api/material';
import { selectableTransferees } from '#/utils/user-options';

const props = defineProps<{
  material: MaterialItem | null;
  projectOwnerId?: null | number;
  searchUsers?: (
    keyword: string,
    keepIds?: Array<null | number | undefined>,
  ) => Promise<void>;
  userOptionsLoading?: boolean;
  users: UserOptionDto[];
}>();
const emit = defineEmits<{ done: [] }>();
const visible = defineModel<boolean>('visible', { default: false });

const userStore = useUserStore();
const saving = ref(false);
const transfereeOptions = computed(() =>
  selectableTransferees(props.users, [
    Number(userStore.userInfo?.userId || 0),
    props.material?.custodianId,
    props.projectOwnerId,
  ]),
);

function searchTransferees(keyword: string) {
  return props.searchUsers?.(keyword, [form.transfereeId]);
}
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
    visible.value = false;
    emit('done');
  } catch {
    // 错误已由 request.ts 拦截器统一弹出
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <ElDialog v-model="visible" title="测试料件流转" width="520px">
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
          :loading="userOptionsLoading"
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
      <ElButton @click="visible = false">取消</ElButton>
      <ElButton :loading="saving" type="primary" @click="submit">提交</ElButton>
    </template>
  </ElDialog>
</template>
