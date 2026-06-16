<script lang="ts" setup>
import type { ApproverType, NodeType, WorkflowItem, WorkflowNode } from '#/api/workflow';

import { nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';

import LogicFlow from '@logicflow/core';
import { RectNode, RectNodeModel } from '@logicflow/core';
import '@logicflow/core/dist/index.css';
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
} from 'element-plus';

import { getWorkflowsApi, saveWorkflowApi } from '#/api/workflow';

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
const TYPE_COLOR: Record<number, string> = {
  0: '#1890ff', 1: '#52c41a', 2: '#722ed1', 3: '#fa8c16', 4: '#eb2f96', 5: '#13c2c2', 6: '#8c8c8c',
};

const loading = ref(false);
const saving = ref(false);
const dialogVisible = ref(false);
const activeBizType = ref('');
const workflows = ref<WorkflowItem[]>([]);
const containerRef = ref<HTMLDivElement>();
const selectedElement = ref<{ id: string; label: string; nodeType?: NodeType; type: 'edge' | 'node' } | null>(null);
let lf: LogicFlow | null = null;
let editingNodeId = '';

const form = reactive({
  approver: '',
  approverType: 0 as ApproverType,
  condition: '',
  name: '',
  signers: [] as string[],
  type: 1 as NodeType,
});

function currentWorkflow() {
  return workflows.value.find((w) => w.bizType === activeBizType.value);
}

function typeLabel(type: NodeType) {
  return nodeTypes.find((i) => i.value === type)?.label ?? '';
}

function registerNode(flow: LogicFlow) {
  class AppNodeModel extends RectNodeModel {
    override setAttributes() {
      this.width = 180;
      this.height = 48;
      this.radius = 6;
    }
    override getNodeStyle() {
      const style: any = super.getNodeStyle();
      const t = (this.properties as any).nodeType ?? 1;
      style.fill = TYPE_COLOR[t] ?? '#52c41a';
      style.stroke = TYPE_COLOR[t] ?? '#52c41a';
      return style;
    }
    override getTextStyle() {
      const style: any = super.getTextStyle();
      style.color = '#ffffff';
      style.fontSize = 13;
      return style;
    }
  }
  flow.register({ type: 'app-node', view: RectNode, model: AppNodeModel });
}

function nodeText(node: WorkflowNode) {
  return `${node.name}\n[${typeLabel(node.type)}]`;
}

function renderWorkflow(wf?: WorkflowItem) {
  if (!lf || !wf) return;
  selectedElement.value = null;
  const nodes = wf.nodes.map((n, i) => ({
    id: n.id,
    type: 'app-node',
    x: n.x ?? 280,
    y: n.y ?? 60 + i * 90,
    text: nodeText(n),
    properties: {
      nodeType: n.type,
      approverType: n.approverType,
      approver: n.approver ?? '',
      signers: n.signers ?? [],
      condition: n.condition ?? '',
    },
  }));
  const edges = wf.nodes.slice(1).map((n, i) => ({
    type: 'polyline',
    sourceNodeId: wf.nodes[i]!.id,
    targetNodeId: n.id,
    text: wf.nodes[i]!.type === 4 ? (wf.nodes[i]!.condition ?? '') : '',
  }));
  lf.render({ nodes, edges });
}

function selectElement(type: 'edge' | 'node', data: any) {
  if (!lf || !data?.id) return;
  lf.selectElementById(data.id);
  if (type === 'edge') {
    selectedElement.value = { id: data.id, label: '连线', type: 'edge' };
    return;
  }

  const label = typeof data.text === 'string' ? data.text : (data.text?.value ?? '');
  selectedElement.value = {
    id: data.id,
    label: label.split('\n')[0] || '节点',
    nodeType: data.properties?.nodeType,
    type: 'node',
  };
}

async function loadData() {
  loading.value = true;
  try {
    workflows.value = await getWorkflowsApi();
    activeBizType.value ||= workflows.value[0]?.bizType ?? '';
    await nextTick();
    renderWorkflow(currentWorkflow());
  } finally {
    loading.value = false;
  }
}

function initLogicFlow() {
  if (!containerRef.value) return;
  lf = new LogicFlow({
    container: containerRef.value,
    grid: true,
    edgeType: 'polyline',
    keyboard: { enabled: true },
  });
  registerNode(lf);
  lf.render({ nodes: [], edges: [] });
  lf.on('node:click', ({ data }: any) => selectElement('node', data));
  lf.on('edge:click', ({ data }: any) => selectElement('edge', data));
  lf.on('node:dbclick', ({ data }: any) => openEdit(data));
  lf.on('blank:click', () => {
    selectedElement.value = null;
  });
  lf.on('node:delete,edge:delete', () => {
    selectedElement.value = null;
  });
  lf.keyboard.on(['backspace', 'delete'], (event: KeyboardEvent) => {
    event.preventDefault();
    deleteSelectedElement();
    return false;
  });
}

function openEdit(node: any) {
  editingNodeId = node.id;
  const p = node.properties ?? {};
  const label =
    typeof node.text === 'string' ? node.text : (node.text?.value ?? '');
  Object.assign(form, {
    approver: p.approver ?? '',
    approverType: p.approverType ?? 0,
    condition: p.condition ?? '',
    name: label.split('\n')[0] || '节点',
    signers: [...(p.signers ?? [])],
    type: p.nodeType ?? 1,
  });
  dialogVisible.value = true;
}

function addNode() {
  if (!lf) return;
  const id = `node_${Date.now()}`;
  lf.addNode({
    id,
    type: 'app-node',
    x: 280,
    y: 140,
    text: `审批节点\n[${typeLabel(1)}]`,
    properties: { nodeType: 1, approverType: 0, approver: '', signers: [], condition: '' },
  });
  ElMessage.info('已添加节点：双击节点配置，拖动节点边缘可连线串联流程');
}

function saveNode() {
  if (!lf) return;
  lf.setProperties(editingNodeId, {
    nodeType: form.type,
    approverType: form.approverType,
    approver: form.approver,
    signers: form.signers.filter(Boolean),
    condition: form.condition,
  });
  lf.updateText(editingNodeId, `${form.name}\n[${typeLabel(form.type)}]`);
  dialogVisible.value = false;
}

function deleteNodeById(nodeId: string, nodeType?: NodeType) {
  if (!lf) return;
  const graph: any = lf.getGraphData();
  const node = (graph.nodes ?? []).find((n: any) => n.id === nodeId);
  const type = nodeType ?? node?.properties?.nodeType;
  if (type === 0 || type === 6) {
    ElMessage.warning('发起和结束节点不能删除');
    return;
  }
  lf.deleteNode(nodeId);
}

function deleteSelectedElement() {
  if (!lf) return;
  const selected: any = lf.getSelectElements(true);
  const selectedEdges = selected.edges ?? [];
  const selectedNodes = selected.nodes ?? [];

  if (selectedEdges.length === 0 && selectedNodes.length === 0) {
    if (selectedElement.value?.type === 'edge') {
      lf.deleteEdge(selectedElement.value.id);
      selectedElement.value = null;
      return;
    }
    if (selectedElement.value?.type === 'node') {
      deleteNodeById(selectedElement.value.id, selectedElement.value.nodeType);
      selectedElement.value = null;
      return;
    }
    ElMessage.info('请先选中节点或连线');
    return;
  }

  selectedEdges.forEach((edge: any) => {
    if (edge.id) lf?.deleteEdge(edge.id);
  });

  selectedNodes.forEach((node: any) => {
    if (!node.id) return;
    deleteNodeById(node.id, node.properties?.nodeType);
  });

  selectedElement.value = null;
}

function validateLinearGraph(nodes: any[], edges: any[]) {
  if (nodes.length < 2) return '流程至少需要发起和结束两个节点';
  if (edges.length !== nodes.length - 1) {
    return '当前流程存在断开的连线，请补齐为一条完整主链后再保存';
  }

  const incoming = new Map<string, number>();
  const outgoing = new Map<string, number>();
  nodes.forEach((n) => {
    incoming.set(n.id, 0);
    outgoing.set(n.id, 0);
  });

  for (const edge of edges) {
    incoming.set(edge.targetNodeId, (incoming.get(edge.targetNodeId) ?? 0) + 1);
    outgoing.set(edge.sourceNodeId, (outgoing.get(edge.sourceNodeId) ?? 0) + 1);
    if ((incoming.get(edge.targetNodeId) ?? 0) > 1 || (outgoing.get(edge.sourceNodeId) ?? 0) > 1) {
      return '当前流程暂只支持单条主链，请去掉多余分支连线';
    }
  }

  const starts = nodes.filter((n) => (incoming.get(n.id) ?? 0) === 0);
  const ends = nodes.filter((n) => (outgoing.get(n.id) ?? 0) === 0);
  if (starts.length !== 1 || ends.length !== 1) {
    return '当前流程必须只有一个起点和一个终点';
  }

  const nextOf = new Map<string, string>();
  edges.forEach((e) => nextOf.set(e.sourceNodeId, e.targetNodeId));
  const seen = new Set<string>();
  let cur = starts[0];
  while (cur && !seen.has(cur.id)) {
    seen.add(cur.id);
    cur = nodes.find((n) => n.id === nextOf.get(cur.id));
  }
  if (seen.size !== nodes.length) {
    return '当前流程存在未串联节点或环形连线，请调整后再保存';
  }

  return '';
}

// 按连线把图排成主干顺序（无并行网关，单链）
function orderNodes(nodes: any[], edges: any[]) {
  const incoming = new Map<string, number>();
  nodes.forEach((n) => incoming.set(n.id, 0));
  edges.forEach((e) => incoming.set(e.targetNodeId, (incoming.get(e.targetNodeId) ?? 0) + 1));
  const nextOf = new Map<string, string>();
  edges.forEach((e) => nextOf.set(e.sourceNodeId, e.targetNodeId));
  const start = nodes.find((n) => (incoming.get(n.id) ?? 0) === 0) ?? nodes[0];
  const ordered: any[] = [];
  const seen = new Set<string>();
  let cur: any = start;
  while (cur && !seen.has(cur.id)) {
    ordered.push(cur);
    seen.add(cur.id);
    cur = nodes.find((n) => n.id === nextOf.get(cur.id));
  }
  nodes.forEach((n) => {
    if (!seen.has(n.id)) ordered.push(n);
  });
  return ordered;
}

async function saveWorkflow() {
  const wf = currentWorkflow();
  if (!wf || !lf) return;
  const graph: any = lf.getGraphData();
  const validationMessage = validateLinearGraph(graph.nodes ?? [], graph.edges ?? []);
  if (validationMessage) {
    ElMessage.warning(validationMessage);
    return;
  }
  const ordered = orderNodes(graph.nodes ?? [], graph.edges ?? []);
  const nodes: WorkflowNode[] = ordered.map((n) => {
    const p = n.properties ?? {};
    const label = typeof n.text === 'string' ? n.text : (n.text?.value ?? '');
    return {
      id: n.id,
      name: label.split('\n')[0] || '节点',
      type: (p.nodeType ?? 1) as NodeType,
      approverType: (p.approverType ?? 0) as ApproverType,
      approver: p.approver || null,
      signers: p.signers?.length ? p.signers : null,
      condition: p.condition || null,
      x: n.x,
      y: n.y,
    };
  });
  saving.value = true;
  try {
    await saveWorkflowApi(wf.id, { bizType: wf.bizType, name: wf.name, nodes });
    ElMessage.success('流程已保存');
    await loadData();
  } finally {
    saving.value = false;
  }
}

watch(activeBizType, async () => {
  await nextTick();
  renderWorkflow(currentWorkflow());
});

onMounted(async () => {
  initLogicFlow();
  await loadData();
});

onBeforeUnmount(() => {
  lf = null;
});
</script>

<template>
  <re-page>
    <div class="space-y-4 p-5">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">审批流程设计器</h2>
          <p class="mt-1 text-sm text-muted-foreground">
            拖拽节点、连线串联流程；双击节点配置审批人 / 会签成员 / 条件。支持借用、转让、归还。
          </p>
        </div>
        <div class="flex flex-wrap justify-end gap-2">
          <ElButton
            :disabled="!selectedElement"
            type="danger"
            plain
            @click="deleteSelectedElement"
          >
            删除选中
          </ElButton>
          <ElButton @click="addNode">+ 添加节点</ElButton>
          <ElButton :loading="saving" type="primary" @click="saveWorkflow">保存流程</ElButton>
        </div>
      </div>

      <div class="rounded border border-dashed border-[var(--el-border-color)] px-3 py-2 text-sm text-muted-foreground">
        当前选中：{{ selectedElement ? `${selectedElement.label}（${selectedElement.type === 'edge' ? '连线' : '节点'}）` : '无' }}。
        可点击节点或连线后按 Delete / Backspace 删除；发起和结束节点固定不可删除。
      </div>

      <ElTabs v-model="activeBizType" v-loading="loading">
        <ElTabPane
          v-for="w in workflows"
          :key="w.id"
          :label="w.name"
          :name="w.bizType"
        />
      </ElTabs>

      <div
        ref="containerRef"
        class="logicflow-canvas"
        style="width: 100%; height: 600px; border: 1px solid var(--el-border-color); border-radius: 8px;"
      ></div>

      <ElDialog v-model="dialogVisible" title="配置节点" width="520px">
        <ElForm label-width="96px">
          <ElFormItem label="节点名称">
            <ElInput v-model="form.name" />
          </ElFormItem>
          <ElFormItem label="节点类型">
            <ElSelect v-model="form.type" style="width: 100%">
              <ElOption v-for="i in nodeTypes" :key="i.value" :label="i.label" :value="i.value" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="审批人来源">
            <ElSelect v-model="form.approverType" style="width: 100%">
              <ElOption v-for="i in approverTypes" :key="i.value" :label="i.label" :value="i.value" />
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
