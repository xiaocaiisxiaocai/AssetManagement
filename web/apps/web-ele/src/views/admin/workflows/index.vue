<script lang="ts" setup>
import type { ApproverType, NodeType, WorkflowItem, WorkflowNode } from '#/api/workflow';

import { computed, onMounted, reactive, ref } from 'vue';

import { getWorkflowsApi, saveWorkflowApi } from '#/api/workflow';

import {
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElOption,
  ElSelect,
  ElTabPane,
  ElTabs,
  ElTag,
} from 'element-plus';

defineOptions({ name: 'AdminWorkflows' });

const nodeTypes: Array<{ label: string; value: NodeType }> = [
  { label: '发起', value: 0 },
  { label: '单人审批', value: 1 },
  { label: '会签', value: 2 },
  { label: '或签', value: 3 },
  { label: '条件', value: 4 },
  { label: '抄送', value: 5 },
  { label: '结束', value: 6 },
];
const approverTypes: Array<{ label: string; value: ApproverType }> = [
  { label: '指定用户', value: 0 },
  { label: '角色', value: 1 },
  { label: '直属主管', value: 2 },
  { label: '部门负责人', value: 3 },
  { label: '发起人自选', value: 4 },
];

type WorkflowNodeForm = Omit<WorkflowNode, 'signers'> & {
  signers: string[];
};

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const activeBizType = ref('');
const workflows = ref<WorkflowItem[]>([]);
const editingIndex = ref(-1);
const form = reactive<WorkflowNodeForm>({
  approver: '',
  approverType: 0,
  condition: '',
  id: '',
  name: '',
  signers: [],
  type: 1,
});

const currentWorkflow = computed(() =>
  workflows.value.find((item) => item.bizType === activeBizType.value),
);

async function loadData() {
  loading.value = true;
  try {
    workflows.value = await getWorkflowsApi();
    activeBizType.value ||= workflows.value[0]?.bizType ?? '';
  } finally {
    loading.value = false;
  }
}

function typeLabel(type: NodeType) {
  return nodeTypes.find((item) => item.value === type)?.label ?? '未知';
}

function openCreate(index: number) {
  editingIndex.value = index;
  Object.assign(form, {
    approver: '',
    approverType: 0,
    condition: '',
    id: `node_${Date.now()}`,
    name: '审批节点',
    signers: [],
    type: 1,
  });
  dialogVisible.value = true;
}

function openEdit(node: WorkflowNode, index: number) {
  editingIndex.value = index;
  Object.assign(form, {
    ...node,
    signers: node.signers ? [...node.signers] : [],
  });
  dialogVisible.value = true;
}

function saveNode() {
  if (!currentWorkflow.value) return;
  const node: WorkflowNode = { ...form, signers: form.signers.filter(Boolean) };
  const nodes = currentWorkflow.value.nodes;
  if (nodes[editingIndex.value]?.id === form.id) {
    nodes.splice(editingIndex.value, 1, node);
  } else {
    nodes.splice(editingIndex.value + 1, 0, node);
  }
  dialogVisible.value = false;
}

function removeNode(index: number) {
  if (!currentWorkflow.value || index === 0 || index === currentWorkflow.value.nodes.length - 1) {
    return;
  }
  currentWorkflow.value.nodes.splice(index, 1);
}

async function saveWorkflow() {
  if (!currentWorkflow.value) return;
  saving.value = true;
  try {
    const workflow = currentWorkflow.value;
    await saveWorkflowApi(workflow.id, {
      bizType: workflow.bizType,
      name: workflow.name,
      nodes: workflow.nodes,
    });
    ElMessage.success('流程已保存');
    await loadData();
  } finally {
    saving.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">审批流程设计器</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            配置借用、转让、归还的审批节点、会签成员与条件分支。
          </p>
        </div>
        <ElButton :loading="saving" type="primary" @click="saveWorkflow">保存流程</ElButton>
      </div>

      <ElTabs v-model="activeBizType" v-loading="loading">
        <ElTabPane
          v-for="workflow in workflows"
          :key="workflow.id"
          :label="workflow.name"
          :name="workflow.bizType"
        />
      </ElTabs>

      <div v-if="currentWorkflow" class="space-y-3">
        <div
          v-for="(node, index) in currentWorkflow.nodes"
          :key="node.id"
          class="rounded border bg-card p-4"
        >
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <div class="flex items-center gap-2">
                <ElTag>{{ index + 1 }}</ElTag>
                <span class="font-medium">{{ node.name }}</span>
                <ElTag type="info">{{ typeLabel(node.type) }}</ElTag>
              </div>
              <div class="mt-2 text-sm text-muted-foreground">
                审批人：{{ node.approver || '无' }}
                <span v-if="node.signers?.length">，会签：{{ node.signers.join('、') }}</span>
                <span v-if="node.condition">，条件：{{ node.condition }}</span>
              </div>
            </div>
            <div class="flex gap-2">
              <ElButton size="small" @click="openCreate(index)">后加节点</ElButton>
              <ElButton size="small" type="primary" @click="openEdit(node, index)">编辑</ElButton>
              <ElButton
                :disabled="index === 0 || index === currentWorkflow.nodes.length - 1"
                size="small"
                type="danger"
                @click="removeNode(index)"
              >
                删除
              </ElButton>
            </div>
          </div>
        </div>
      </div>

      <ElDialog v-model="dialogVisible" title="编辑节点" width="520px">
        <ElForm label-width="96px">
          <ElFormItem label="节点名称">
            <ElInput v-model="form.name" />
          </ElFormItem>
          <ElFormItem label="节点类型">
            <ElSelect v-model="form.type" style="width: 100%">
              <ElOption
                v-for="item in nodeTypes"
                :key="item.value"
                :label="item.label"
                :value="item.value"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="审批人来源">
            <ElSelect v-model="form.approverType" style="width: 100%">
              <ElOption
                v-for="item in approverTypes"
                :key="item.value"
                :label="item.label"
                :value="item.value"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="审批人">
            <ElInput v-model="form.approver" placeholder="用户、角色或说明" />
          </ElFormItem>
          <ElFormItem label="会签成员">
            <ElSelect
              v-model="form.signers"
              allow-create
              filterable
              multiple
              placeholder="输入成员后回车"
              style="width: 100%"
            />
          </ElFormItem>
          <ElFormItem label="条件表达式">
            <ElInput v-model="form.condition" placeholder="如 amount>5000" />
          </ElFormItem>
        </ElForm>
        <template #footer>
          <ElButton @click="dialogVisible = false">取消</ElButton>
          <ElButton type="primary" @click="saveNode">确定</ElButton>
        </template>
      </ElDialog>
    </div>
  </re-page>
</template>
