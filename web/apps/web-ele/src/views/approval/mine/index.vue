<script lang="ts" setup>
import type { AssetItem } from '#/api/asset';
import type { ApprovalFlow } from '#/api/workflow';

import { onMounted, reactive, ref } from 'vue';

import { getAssetListApi } from '#/api/asset';
import { getMineApprovalsApi, startApprovalApi } from '#/api/workflow';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElOption,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'ApprovalMine' });

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const flows = ref<ApprovalFlow[]>([]);
const assets = ref<AssetItem[]>([]);
const form = reactive({
  assetId: undefined as number | undefined,
  bizType: 'borrow',
  reason: '',
  returnDate: '',
});

async function loadData() {
  loading.value = true;
  try {
    const [mine, assetPage] = await Promise.all([
      getMineApprovalsApi(),
      getAssetListApi({ page: 1, pageSize: 200 }),
    ]);
    flows.value = mine;
    assets.value = assetPage.items;
  } finally {
    loading.value = false;
  }
}

function openStart(type = 'borrow') {
  Object.assign(form, {
    assetId: undefined,
    bizType: type,
    reason: '',
    returnDate: '',
  });
  dialogVisible.value = true;
}

async function submit() {
  if (!form.assetId) {
    ElMessage.warning('请选择资产');
    return;
  }
  saving.value = true;
  try {
    await startApprovalApi({
      assetId: form.assetId,
      bizType: form.bizType,
      reason: form.reason,
      returnDate: form.returnDate,
    });
    ElMessage.success('申请已提交');
    dialogVisible.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

function bizText(type: string) {
  return { borrow: '借用', return: '归还', transfer: '转让' }[type] ?? type;
}

function statusText(status: string) {
  return { approved: '已通过', pending: '审批中', rejected: '已驳回' }[status] ?? status;
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">我的申请</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            发起并跟踪本人提交的资产流程。
          </p>
        </div>
        <div class="flex gap-2">
          <ElButton type="success" @click="openStart('borrow')">发起借用</ElButton>
          <ElButton type="warning" @click="openStart('transfer')">发起转让</ElButton>
          <ElButton @click="openStart('return')">发起归还</ElButton>
        </div>
      </div>

      <ElTable v-loading="loading" :data="flows" border>
        <ElTableColumn label="流程编号" min-width="180" prop="flowNo" />
        <ElTableColumn label="类型" width="90">
          <template #default="{ row }">
            <ElTag>{{ bizText(row.bizType) }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="资产" min-width="180" prop="assetName" />
        <ElTableColumn label="当前节点" min-width="140">
          <template #default="{ row }">
            {{ row.nodes[row.currentNodeIndex]?.name || '-' }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="状态" width="100">
          <template #default="{ row }">
            {{ statusText(row.status) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="事由" min-width="220" prop="reason" />
      </ElTable>

      <ElDialog v-model="dialogVisible" title="发起申请" width="520px">
        <ElForm label-width="88px">
          <ElFormItem label="申请类型">
            <ElSelect v-model="form.bizType" style="width: 100%">
              <ElOption label="借用" value="borrow" />
              <ElOption label="转让" value="transfer" />
              <ElOption label="归还" value="return" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="资产">
            <ElSelect v-model="form.assetId" filterable placeholder="选择资产" style="width: 100%">
              <ElOption
                v-for="asset in assets"
                :key="asset.id"
                :label="`${asset.assetNo} / ${asset.name}`"
                :value="asset.id"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="归还日期">
            <ElInput v-model="form.returnDate" placeholder="YYYY-MM-DD，借用时填写" />
          </ElFormItem>
          <ElFormItem label="申请事由">
            <ElInput v-model="form.reason" :rows="3" type="textarea" />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton :loading="saving" type="primary" @click="submit">提交</ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>
