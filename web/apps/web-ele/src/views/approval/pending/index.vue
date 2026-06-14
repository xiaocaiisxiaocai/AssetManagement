<script lang="ts" setup>
import type { ApprovalFlow, FlowNode } from '#/api/workflow';

import { onMounted, ref } from 'vue';

import {
  addSignApi,
  approveFlowApi,
  getPendingApprovalsApi,
  rejectFlowApi,
  transferSignApi,
} from '#/api/workflow';

import {
  ElButton,
  ElDialog,
  ElInput,
  ElMessage,
  ElTag,
  ElTimeline,
  ElTimelineItem,
} from 'element-plus';

defineOptions({ name: 'ApprovalPending' });

const loading = ref(false);
const actionLoading = ref(false);
const detailVisible = ref(false);
const selected = ref<ApprovalFlow | null>(null);
const flows = ref<ApprovalFlow[]>([]);
const opinion = ref('同意');
const signer = ref('');
const extraUser = ref('');

const nodeStatusText = ['待办', '当前', '已办', '跳过', '驳回'];
const nodeTypeText = ['发起', '审批', '会签', '或签', '条件', '抄送', '结束'];

async function loadData() {
  loading.value = true;
  try {
    flows.value = await getPendingApprovalsApi();
  } finally {
    loading.value = false;
  }
}

function openDetail(flow: ApprovalFlow) {
  selected.value = flow;
  const node = currentNode(flow);
  signer.value = node?.signers?.[0] ?? '';
  opinion.value = '同意';
  extraUser.value = '';
  detailVisible.value = true;
}

function currentNode(flow: ApprovalFlow): FlowNode | undefined {
  return flow.nodes[flow.currentNodeIndex];
}

async function approve() {
  if (!selected.value) return;
  actionLoading.value = true;
  try {
    await approveFlowApi(selected.value.id, {
      opinion: opinion.value,
      signer: signer.value || undefined,
    });
    ElMessage.success('已通过');
    detailVisible.value = false;
    await loadData();
  } finally {
    actionLoading.value = false;
  }
}

async function reject() {
  if (!selected.value || !opinion.value.trim()) {
    ElMessage.warning('请填写驳回理由');
    return;
  }
  actionLoading.value = true;
  try {
    await rejectFlowApi(selected.value.id, opinion.value);
    ElMessage.success('已驳回');
    detailVisible.value = false;
    await loadData();
  } finally {
    actionLoading.value = false;
  }
}

async function addSign() {
  if (!selected.value || !extraUser.value.trim()) return;
  await addSignApi(selected.value.id, extraUser.value.trim());
  ElMessage.success('已加签');
  await loadData();
}

async function transferSign() {
  if (!selected.value || !extraUser.value.trim()) return;
  await transferSignApi(selected.value.id, extraUser.value.trim());
  ElMessage.success('已转签');
  await loadData();
}

function bizText(type: string) {
  return { borrow: '借用', return: '归还', transfer: '转让' }[type] ?? type;
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold">待我审批</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            处理资产借用、转让和归还流程。
          </p>
        </div>
        <ElButton @click="loadData">刷新</ElButton>
      </div>

      <div v-loading="loading" class="grid gap-3">
        <div
          v-for="flow in flows"
          :key="flow.id"
          class="rounded border bg-card p-4"
        >
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div class="flex items-center gap-2">
                <ElTag>{{ bizText(flow.bizType) }}</ElTag>
                <span class="font-medium">{{ flow.assetName }}</span>
                <span class="text-sm text-muted-foreground">{{ flow.flowNo }}</span>
              </div>
              <div class="mt-2 text-sm text-muted-foreground">
                发起人：{{ flow.applicant }}，当前节点：{{ currentNode(flow)?.name }}
              </div>
              <div class="mt-1 text-sm text-muted-foreground">
                事由：{{ flow.reason || '无' }}
              </div>
            </div>
            <ElButton type="primary" @click="openDetail(flow)">处理</ElButton>
          </div>
        </div>
        <div v-if="!flows.length" class="rounded border bg-card p-10 text-center text-muted-foreground">
          暂无待审批工单
        </div>
      </div>

      <ElDialog v-model="detailVisible" title="审批详情" width="760px">
        <template v-if="selected">
          <div class="mb-4 rounded border p-3 text-sm">
            <div class="font-medium">{{ selected.assetName }}（{{ selected.assetNo }}）</div>
            <div class="mt-1 text-muted-foreground">
              {{ bizText(selected.bizType) }} / {{ selected.status }} / 金额 {{ selected.amount }}
            </div>
          </div>
          <ElTimeline>
            <ElTimelineItem
              v-for="(node, index) in selected.nodes"
              :key="`${node.name}-${index}`"
              :timestamp="node.time || nodeStatusText[node.status]"
            >
              <div class="font-medium">
                {{ node.name }}
                <ElTag class="ml-2" size="small">{{ nodeTypeText[node.type] }}</ElTag>
              </div>
              <div class="mt-1 text-sm text-muted-foreground">
                审批人：{{ node.approver || node.signers?.join('、') || '无' }}
                <span v-if="node.opinion">，意见：{{ node.opinion }}</span>
              </div>
            </ElTimelineItem>
          </ElTimeline>
          <div class="mt-4 grid gap-3">
            <ElInput v-model="signer" placeholder="会签/或签时填写签署人，可留空" />
            <ElInput v-model="opinion" placeholder="审批意见或驳回理由" />
            <div class="flex flex-wrap gap-2">
              <ElButton :loading="actionLoading" type="primary" @click="approve">通过</ElButton>
              <ElButton :loading="actionLoading" type="danger" @click="reject">驳回</ElButton>
            </div>
            <div class="flex flex-wrap gap-2">
              <ElInput v-model="extraUser" placeholder="加签/转签人员" style="width: 220px" />
              <ElButton @click="addSign">加签</ElButton>
              <ElButton @click="transferSign">转签</ElButton>
            </div>
          </div>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>
