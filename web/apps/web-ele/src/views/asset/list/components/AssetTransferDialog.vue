<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';
import type { UserOptionDto } from '#/api/user';

import { computed, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

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

import { startApprovalApi } from '#/api/workflow';
import { selectableTransferees } from '#/utils/user-options';

const props = defineProps<{
  asset: AssetItem | null;
  searchUsers?: (
    keyword: string,
    keepIds?: Array<null | number | undefined>,
  ) => Promise<void>;
  userOptionsLoading?: boolean;
  users: UserOptionDto[];
}>();

const emit = defineEmits<{ submitted: [] }>();

const router = useRouter();
const userStore = useUserStore();
const transfereeOptions = computed(() =>
  selectableTransferees(props.users, [
    Number(userStore.userInfo?.userId || 0),
    props.asset?.custodianId,
  ]),
);

function searchTransferees(keyword: string) {
  return props.searchUsers?.(keyword, [form.transfereeId]);
}

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
  if (
    props.asset.custodianId &&
    form.transfereeId === props.asset.custodianId
  ) {
    ElMessage.warning('受让人不能是当前持有人');
    return;
  }
  const reason = form.reason.trim();
  if (!reason) {
    ElMessage.warning('请填写转让原因');
    return;
  }
  if (reason.length > 500) {
    ElMessage.warning('转让原因不能超过 500 个字符');
    return;
  }
  saving.value = true;
  try {
    await startApprovalApi({
      assetId: props.asset.id,
      bizType: 'transfer',
      reason,
      transfereeId: form.transfereeId,
    });
    ElMessage.success('转让申请已提交');
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
      <ElFormItem label="转让原因" required>
        <ElInput
          v-model="form.reason"
          :maxlength="500"
          :rows="3"
          clearable
          placeholder="请填写转让原因，最多 500 字"
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
