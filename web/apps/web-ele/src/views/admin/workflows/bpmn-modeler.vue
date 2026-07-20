<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref, shallowRef } from 'vue';

import BpmnModeler from 'bpmn-js/lib/Modeler';
import { ElButton, ElButtonGroup, ElMessage } from 'element-plus';

import BpmnProperties from './bpmn-properties.vue';
import {
  getBranchLabelDelta,
  getGatewayValidationError,
  getSuggestedBranchName,
  isGatewayDiagramNode,
  resolveDiagramElement,
} from './gateway-branches';

import 'bpmn-js/dist/assets/bpmn-font/css/bpmn-embedded.css';
import 'bpmn-js/dist/assets/bpmn-js.css';
import 'bpmn-js/dist/assets/diagram-js.css';

interface Props {
  workflowId: number;
  initialXml?: string;
}

defineOptions({ name: 'BpmnModeler' });

const props = defineProps<Props>();

const emit = defineEmits<{
  save: [bpmnXml: string];
}>();

const loading = ref(false);
const saving = ref(false);
const containerRef = ref<HTMLDivElement>();
const modeler = shallowRef<any>();
const selectedElement = shallowRef<any>(null); // 当前选中的元素
let branchNormalizationQueued = false;
let isNormalizingBranches = false;

const elementTypeText = computed(() => {
  const type = selectedElement.value?.businessObject?.$type;
  const map: Record<string, string> = {
    'bpmn:EndEvent': '结束',
    'bpmn:ExclusiveGateway': '条件分支',
    'bpmn:InclusiveGateway': '包容分支',
    'bpmn:ParallelGateway': '并行审批',
    'bpmn:ServiceTask': '自动任务',
    'bpmn:SequenceFlow': '流转线',
    'bpmn:StartEvent': '发起',
    'bpmn:UserTask': '审批节点',
  };
  return type ? map[type] || '流程元素' : '未选择';
});

const selectedElementText = computed(() => {
  const businessObject = selectedElement.value?.businessObject;
  const name = businessObject?.name || selectedElement.value?.id;
  return name ? `${name} · ${elementTypeText.value}` : elementTypeText.value;
});

const allowedPaletteEntries = new Set([
  'create.end-event',
  'create.exclusive-gateway',
  'create.inclusive-gateway',
  'create.parallel-gateway',
  'create.service-task',
  'create.start-event',
  'create.task',
  'global-connect-tool',
  'hand-tool',
  'lasso-tool',
  'tool-separator',
]);

const allowedContextPadEntries = new Set([
  'append.append-task',
  'append.end-event',
  'append.gateway',
  'connect',
  'delete',
]);

// 空白 BPMN 模板
const emptyBpmnTemplate = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
                  xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"
                  xmlns:di="http://www.omg.org/spec/DD/20100524/DI"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  id="Definitions_1"
                  targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_1" isExecutable="true">
    <bpmn:startEvent id="StartEvent_1" name="发起申请">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:endEvent id="EndEvent_1" name="流程结束">
      <bpmn:incoming>Flow_2</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:userTask id="Activity_1" name="部门经理审批" camunda:assignee="deptManager">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:sequenceFlow id="Flow_1" sourceRef="StartEvent_1" targetRef="Activity_1" />
    <bpmn:sequenceFlow id="Flow_2" sourceRef="Activity_1" targetRef="EndEvent_1" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
      <bpmndi:BPMNShape id="StartEvent_1_di" bpmnElement="StartEvent_1">
        <dc:Bounds x="152" y="102" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="EndEvent_1_di" bpmnElement="EndEvent_1">
        <dc:Bounds x="432" y="102" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="Activity_1_di" bpmnElement="Activity_1">
        <dc:Bounds x="260" y="80" width="100" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id="Flow_1_di" bpmnElement="Flow_1">
        <di:waypoint x="188" y="120" />
        <di:waypoint x="260" y="120" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="Flow_2_di" bpmnElement="Flow_2">
        <di:waypoint x="360" y="120" />
        <di:waypoint x="432" y="120" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>`;

function installWorkflowPalette(modelerInstance: any) {
  const palette = modelerInstance.get('palette');
  const contextPad = modelerInstance.get('contextPad');
  const elementFactory = modelerInstance.get('elementFactory');
  const create = modelerInstance.get('create');
  const autoPlace = modelerInstance.get('autoPlace', false);

  palette.registerProvider(500, {
    getPaletteEntries() {
      return (entries: Record<string, any>) => {
        const nextEntries: Record<string, any> = {};
        for (const key of allowedPaletteEntries) {
          if (entries[key]) nextEntries[key] = entries[key];
        }

        if (nextEntries['create.exclusive-gateway']) {
          nextEntries['create.exclusive-gateway'] = {
            ...nextEntries['create.exclusive-gateway'],
            className: 'bpmn-icon-gateway-xor',
            title: '创建条件网关',
          };
          nextEntries['create.parallel-gateway'] = createShapeEntry(
            'bpmn:ParallelGateway',
            'gateway',
            'bpmn-icon-gateway-parallel',
            '创建并行网关',
          );
        }

        if (nextEntries['create.task']) {
          nextEntries['create.task'] = createShapeEntry(
            'bpmn:UserTask',
            'activity',
            'bpmn-icon-user-task',
            '创建审批节点',
          );
        }

        nextEntries['create.inclusive-gateway'] = createShapeEntry(
          'bpmn:InclusiveGateway',
          'gateway',
          'bpmn-icon-gateway-or',
          '创建包容网关',
        );
        nextEntries['create.service-task'] = createShapeEntry(
          'bpmn:ServiceTask',
          'activity',
          'bpmn-icon-service-task',
          '创建自动任务',
        );

        return nextEntries;
      };
    },
  });

  function createShapeEntry(
    type: string,
    group: string,
    className: string,
    title: string,
  ) {
    const createShape = (event: Event) => {
      const shape = elementFactory.createShape({ type });
      create.start(event, shape);
    };

    return {
      group,
      className,
      title,
      action: {
        click: createShape,
        dragstart: createShape,
      },
    };
  }

  contextPad.registerProvider(500, {
    getContextPadEntries() {
      return (entries: Record<string, any>) => {
        const nextEntries: Record<string, any> = {};
        for (const key of allowedContextPadEntries) {
          if (entries[key]) nextEntries[key] = entries[key];
        }

        if (nextEntries['append.append-task']) {
          const appendUserTaskStart = (event: Event, source: any) => {
            const shape = elementFactory.createShape({ type: 'bpmn:UserTask' });
            create.start(event, shape, { source });
          };
          const appendUserTask = (_: Event, source: any) => {
            const shape = elementFactory.createShape({ type: 'bpmn:UserTask' });
            if (autoPlace) {
              autoPlace.append(source, shape);
            } else {
              appendUserTaskStart(_, source);
            }
          };
          nextEntries['append.append-task'] = {
            ...nextEntries['append.append-task'],
            className: 'bpmn-icon-user-task',
            title: '追加审批节点',
            action: {
              click: appendUserTask,
              dragstart: appendUserTaskStart,
            },
          };
        }

        if (nextEntries['append.gateway']) {
          nextEntries['append.gateway'] = {
            ...nextEntries['append.gateway'],
            title: '追加条件/并行网关',
          };
        }

        return nextEntries;
      };
    },
  });
}

function normalizeGatewayBranchLabels() {
  if (!modeler.value || isNormalizingBranches) return 0;

  const elementRegistry = modeler.value.get('elementRegistry');
  const modeling = modeler.value.get('modeling');
  const gateways = elementRegistry
    .getAll()
    .filter((element: any) => isGatewayDiagramNode(element));
  let normalizedCount = 0;

  isNormalizingBranches = true;
  try {
    for (const gateway of gateways) {
      const gatewayType = gateway.businessObject.$type;
      const explicitDefault = gateway.businessObject.default;
      for (const [index, flow] of (gateway.outgoing || []).entries()) {
        const businessObject = flow.businessObject;
        if (businessObject?.name?.trim()) continue;

        const isExplicitDefault =
          explicitDefault === businessObject ||
          explicitDefault === businessObject?.id ||
          explicitDefault?.id === businessObject?.id;
        modeling.updateProperties(flow, {
          name: getSuggestedBranchName({
            conditionExpression: businessObject?.conditionExpression?.body,
            gatewayType,
            index,
            isExplicitDefault,
            targetId: flow.target?.id,
            targetName: flow.target?.businessObject?.name,
            targetType: flow.target?.businessObject?.$type,
          }),
        });
        if (flow.label && flow.waypoints?.length > 1) {
          const delta = getBranchLabelDelta(flow.waypoints, flow.label, {
            x: gateway.x + gateway.width / 2,
            y: gateway.y + gateway.height / 2,
          });
          if (Math.abs(delta.x) > 1 || Math.abs(delta.y) > 1) {
            modeling.moveShape(flow.label, delta);
          }
        }
        normalizedCount += 1;
      }
    }
  } finally {
    isNormalizingBranches = false;
  }

  return normalizedCount;
}

function scheduleGatewayBranchNormalization() {
  if (branchNormalizationQueued || isNormalizingBranches) return;

  branchNormalizationQueued = true;
  queueMicrotask(() => {
    branchNormalizationQueued = false;
    normalizeGatewayBranchLabels();
  });
}

function validateGatewayBranches() {
  const elementRegistry = modeler.value.get('elementRegistry');
  let validationError = '';
  const invalidGateway = elementRegistry.getAll().find((element: any) => {
    if (!isGatewayDiagramNode(element)) return false;

    const gatewayType = element.businessObject?.$type;
    if (
      gatewayType !== 'bpmn:ExclusiveGateway' &&
      gatewayType !== 'bpmn:ParallelGateway'
    ) {
      return false;
    }

    const error = getGatewayValidationError({
      expressions: (element.outgoing || []).map(
        (flow: any) => flow.businessObject?.conditionExpression?.body || '',
      ),
      gatewayName: element.businessObject?.name || element.id,
      gatewayType,
    });
    if (!error) return false;
    validationError = error;
    return true;
  });

  if (!invalidGateway) return true;
  modeler.value.get('selection').select(invalidGateway);
  modeler.value.get('canvas').scrollToElement(invalidGateway);
  ElMessage.error(validationError);
  return false;
}

async function initModeler() {
  if (!containerRef.value) return;

  loading.value = true;
  try {
    modeler.value = new BpmnModeler({
      container: containerRef.value,
      keyboard: { bindTo: document },
    });
    installWorkflowPalette(modeler.value);
    if (import.meta.env.DEV) {
      (window as any).__bpmnModeler = modeler.value;
    }

    // 加载初始 XML 或空白模板
    const xml = props.initialXml || emptyBpmnTemplate;
    await modeler.value.importXML(xml);
    const normalizedBranchCount = normalizeGatewayBranchLabels();
    if (normalizedBranchCount > 0) {
      ElMessage.info(
        `已为 ${normalizedBranchCount} 条旧分支补充画布标签，保存后将写入流程定义`,
      );
    }

    // 自动适配画布大小
    const canvas = modeler.value.get('canvas');
    canvas.zoom('fit-viewport');

    // 监听元素选中事件
    const eventBus = modeler.value.get('eventBus');
    eventBus.on('selection.changed', (event: any) => {
      const { newSelection } = event;
      selectedElement.value =
        newSelection && newSelection.length > 0
          ? resolveDiagramElement(newSelection[0])
          : null;
    });
    eventBus.on('commandStack.changed', scheduleGatewayBranchNormalization);
  } catch (error: any) {
    console.error('初始化 BPMN 设计器失败:', error);
    ElMessage.error(error.message || '初始化失败');
  } finally {
    loading.value = false;
  }
}

async function handleSave() {
  if (!modeler.value) return;

  saving.value = true;
  try {
    normalizeGatewayBranchLabels();
    if (!validateGatewayBranches()) return;
    const { xml } = await modeler.value.saveXML({ format: true });
    emit('save', xml);
  } catch (error: any) {
    console.error('保存 BPMN XML 失败:', error);
    ElMessage.error(error.message || '保存失败');
  } finally {
    saving.value = false;
  }
}

async function handleDownload() {
  if (!modeler.value) return;

  try {
    const { xml } = await modeler.value.saveXML({ format: true });
    const blob = new Blob([xml], { type: 'application/xml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `workflow-${props.workflowId}.bpmn`;
    a.click();
    URL.revokeObjectURL(url);
    ElMessage.success('下载成功');
  } catch (error: any) {
    console.error('下载失败:', error);
    ElMessage.error(error.message || '下载失败');
  }
}

async function handleZoomIn() {
  if (!modeler.value) return;
  const canvas = modeler.value.get('canvas');
  canvas.zoom(canvas.zoom() + 0.1);
}

async function handleZoomOut() {
  if (!modeler.value) return;
  const canvas = modeler.value.get('canvas');
  canvas.zoom(canvas.zoom() - 0.1);
}

async function handleZoomReset() {
  if (!modeler.value) return;
  const canvas = modeler.value.get('canvas');
  canvas.zoom('fit-viewport');
}

function createShape(type: string) {
  const elementFactory = modeler.value.get('elementFactory');
  return elementFactory.createShape({ type });
}

function addShapeToCanvas(type: string) {
  if (!modeler.value) return;

  const canvas = modeler.value.get('canvas');
  const modeling = modeler.value.get('modeling');
  const selection = modeler.value.get('selection');
  const rootElement = canvas.getRootElement();
  const viewbox = canvas.viewbox();
  const childCount = rootElement.children?.length ?? 0;
  const stagger = (childCount % 5) * 24;
  const shape = createShape(type);
  const createdShape = modeling.createShape(
    shape,
    {
      x: viewbox.x + viewbox.width / 2 + stagger,
      y: viewbox.y + viewbox.height / 2 + stagger,
    },
    rootElement,
  );

  selection.select(createdShape);
  canvas.scrollToElement(createdShape);
}

function startCreateShape(event: DragEvent | MouseEvent, type: string) {
  if (!modeler.value) return;
  const create = modeler.value.get('create');
  create.start(event, createShape(type));
}

function handleNodeCardKeydown(event: KeyboardEvent, type: string) {
  if (event.key !== 'Enter' && event.key !== ' ') return;

  event.preventDefault();
  addShapeToCanvas(type);
}

onMounted(() => {
  initModeler();
});

onUnmounted(() => {
  if (import.meta.env.DEV) {
    delete (window as any).__bpmnModeler;
  }
  modeler.value?.destroy();
  modeler.value = undefined;
  selectedElement.value = null;
});
</script>

<template>
  <div class="bpmn-modeler-wrapper">
    <div class="designer-toolbar">
      <div class="toolbar-brand">
        <span class="brand-mark">流程</span>
        <div>
          <div class="brand-title">审批流程设计</div>
          <div class="brand-subtitle">标准 BPMN 模型，业务化中文配置</div>
        </div>
      </div>
      <div class="toolbar-center">
        <span class="tool-hint">左侧拖拽节点，右侧配置审批人与条件</span>
      </div>
      <div class="toolbar-actions">
        <ElButtonGroup>
          <ElButton @click="handleZoomOut">缩小</ElButton>
          <ElButton @click="handleZoomReset">适应</ElButton>
          <ElButton @click="handleZoomIn">放大</ElButton>
        </ElButtonGroup>
        <ElButton @click="handleDownload"> 下载 </ElButton>
        <ElButton :loading="saving" type="primary" @click="handleSave">
          保存
        </ElButton>
      </div>
    </div>

    <div class="designer-content">
      <aside class="designer-sidebar">
        <div class="sidebar-title">组件库</div>
        <div
          aria-label="添加发起节点到画布"
          class="node-card node-start"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startCreateShape($event, 'bpmn:StartEvent')"
          @keydown="handleNodeCardKeydown($event, 'bpmn:StartEvent')"
        >
          <span class="node-icon bpmn-icon-start-event-none"></span>
          <div>
            <strong>发起</strong>
            <span>流程开始节点</span>
          </div>
        </div>
        <div
          aria-label="添加审批节点到画布"
          class="node-card node-approval"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startCreateShape($event, 'bpmn:UserTask')"
          @keydown="handleNodeCardKeydown($event, 'bpmn:UserTask')"
        >
          <span class="node-icon bpmn-icon-user-task"></span>
          <div>
            <strong>审批节点</strong>
            <span>配置审批人、会签方式</span>
          </div>
        </div>
        <div
          aria-label="添加条件分支到画布"
          class="node-card node-gateway"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startCreateShape($event, 'bpmn:ExclusiveGateway')"
          @keydown="handleNodeCardKeydown($event, 'bpmn:ExclusiveGateway')"
        >
          <span class="node-icon bpmn-icon-gateway-xor"></span>
          <div>
            <strong>条件分支</strong>
            <span>按部门、角色等条件流转</span>
          </div>
        </div>
        <div
          aria-label="添加并行审批到画布"
          class="node-card node-parallel"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startCreateShape($event, 'bpmn:ParallelGateway')"
          @keydown="handleNodeCardKeydown($event, 'bpmn:ParallelGateway')"
        >
          <span class="node-icon bpmn-icon-gateway-parallel"></span>
          <div>
            <strong>并行审批</strong>
            <span>多个分支同时处理</span>
          </div>
        </div>
        <div
          aria-label="添加包容分支到画布"
          class="node-card node-gateway"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startCreateShape($event, 'bpmn:InclusiveGateway')"
          @keydown="handleNodeCardKeydown($event, 'bpmn:InclusiveGateway')"
        >
          <span class="node-icon bpmn-icon-gateway-or"></span>
          <div>
            <strong>包容分支</strong>
            <span>同时进入所有满足条件的分支</span>
          </div>
        </div>
        <div
          aria-label="添加自动任务到画布"
          class="node-card node-approval"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startCreateShape($event, 'bpmn:ServiceTask')"
          @keydown="handleNodeCardKeydown($event, 'bpmn:ServiceTask')"
        >
          <span class="node-icon bpmn-icon-service-task"></span>
          <div>
            <strong>自动任务</strong>
            <span>无需人工审批并自动继续</span>
          </div>
        </div>
        <div
          aria-label="添加结束节点到画布"
          class="node-card node-end"
          draggable="true"
          role="button"
          tabindex="0"
          @dragstart="startCreateShape($event, 'bpmn:EndEvent')"
          @keydown="handleNodeCardKeydown($event, 'bpmn:EndEvent')"
        >
          <span class="node-icon bpmn-icon-end-event-none"></span>
          <div>
            <strong>结束</strong>
            <span>流程终止节点</span>
          </div>
        </div>

        <div class="sidebar-note">
          从这里拖拽组件到画布，或聚焦后按
          Enter/空格直接添加，也可以使用画布左上角的标准工具栏。
        </div>
      </aside>

      <main class="canvas-shell">
        <div class="canvas-statusbar">
          <span>当前选中：{{ selectedElementText }}</span>
          <span>网关分支名称会同步显示在画布连线上</span>
        </div>
        <div
          ref="containerRef"
          class="bpmn-container"
          element-loading-text="正在载入流程设计器..."
          v-loading="loading"
        ></div>
      </main>

      <div class="properties-panel">
        <BpmnProperties :element="selectedElement" :modeler="modeler" />
      </div>
    </div>
  </div>
</template>

<style>
/* 全局样式，不使用 scoped，确保能覆盖 bpmn-js */
.bpmn-modeler-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  color: var(--workflow-text);
  background: var(--workflow-page-bg);
  --workflow-page-bg: #eef3f9;
  --workflow-panel-bg: #ffffff;
  --workflow-panel-soft-bg: #f8fbff;
  --workflow-section-bg: #fbfdff;
  --workflow-card-bg: #f8fbff;
  --workflow-card-icon-bg: #eaf2ff;
  --workflow-border: #d8e0eb;
  --workflow-border-soft: #dce6f3;
  --workflow-border-dashed: #c8d5e6;
  --workflow-text: #23324d;
  --workflow-title: #1f3f6d;
  --workflow-muted: #6a7890;
  --workflow-muted-strong: #63748c;
  --workflow-primary: #1d5fbf;
  --workflow-grid-line: #dfe7f1;
  --workflow-shadow: 0 1px 4px rgb(31 63 109 / 6%);
}

:global(.dark) .bpmn-modeler-wrapper {
  --workflow-page-bg: var(--el-bg-color-page);
  --workflow-panel-bg: var(--el-bg-color);
  --workflow-panel-soft-bg: rgb(34 37 43);
  --workflow-section-bg: rgb(32 35 40);
  --workflow-card-bg: var(--el-fill-color-light);
  --workflow-card-icon-bg: rgb(42 47 55);
  --workflow-border: rgb(64 68 77);
  --workflow-border-soft: rgb(58 62 70);
  --workflow-border-dashed: rgb(72 76 86);
  --workflow-text: var(--el-text-color-primary);
  --workflow-title: var(--el-text-color-primary);
  --workflow-muted: var(--el-text-color-secondary);
  --workflow-muted-strong: var(--el-text-color-regular);
  --workflow-primary: var(--el-color-primary);
  --workflow-grid-line: rgb(255 255 255 / 5%);
  --workflow-shadow: none;
}

.bpmn-modeler-wrapper .designer-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 56px;
  padding: 0 14px;
  background: var(--workflow-panel-bg);
  border-bottom: 1px solid var(--workflow-border);
  box-shadow: var(--workflow-shadow);
}

.bpmn-modeler-wrapper .toolbar-brand,
.bpmn-modeler-wrapper .toolbar-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.bpmn-modeler-wrapper .brand-mark {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  color: #ffffff;
  font-size: 13px;
  font-weight: 600;
  background: var(--workflow-primary);
  border-radius: 4px;
}

.bpmn-modeler-wrapper .brand-title {
  color: var(--workflow-title);
  font-size: 15px;
  font-weight: 600;
  line-height: 18px;
}

.bpmn-modeler-wrapper .brand-subtitle,
.bpmn-modeler-wrapper .tool-hint {
  color: var(--workflow-muted);
  font-size: 12px;
  line-height: 16px;
}

.bpmn-modeler-wrapper .designer-content {
  flex: 1;
  display: flex;
  min-height: 0;
  padding: 10px;
  gap: 10px;
  overflow: hidden;
}

.bpmn-modeler-wrapper .designer-sidebar {
  width: 196px;
  flex: 0 0 196px;
  padding: 12px;
  overflow-y: auto;
  background: var(--workflow-panel-bg);
  border: 1px solid var(--workflow-border);
  border-radius: 4px;
  box-shadow: var(--workflow-shadow);
}

.bpmn-modeler-wrapper .sidebar-title {
  margin-bottom: 10px;
  color: var(--workflow-title);
  font-size: 13px;
  font-weight: 600;
}

.bpmn-modeler-wrapper .node-card {
  display: flex;
  align-items: center;
  gap: 9px;
  min-height: 58px;
  padding: 8px;
  margin-bottom: 8px;
  background: var(--workflow-card-bg);
  border: 1px solid var(--workflow-border-soft);
  border-left: 3px solid #2f72d0;
  border-radius: 4px;
  cursor: grab;
  user-select: none;
}

.bpmn-modeler-wrapper .node-card:hover,
.bpmn-modeler-wrapper .node-card:focus-visible {
  background: var(--workflow-card-icon-bg);
  border-color: var(--workflow-primary);
  outline: none;
}

.bpmn-modeler-wrapper .node-card:focus-visible {
  box-shadow: 0 0 0 2px
    color-mix(in srgb, var(--workflow-primary) 24%, transparent);
}

.bpmn-modeler-wrapper .node-card:active {
  cursor: grabbing;
}

.bpmn-modeler-wrapper .node-card strong {
  display: block;
  color: var(--workflow-text);
  font-size: 13px;
  font-weight: 600;
  line-height: 18px;
}

.bpmn-modeler-wrapper .node-card span:not(.node-icon) {
  display: block;
  color: var(--workflow-muted);
  font-size: 12px;
  line-height: 17px;
}

.bpmn-modeler-wrapper .node-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  color: var(--workflow-primary);
  font-size: 18px;
  background: var(--workflow-card-icon-bg);
  border-radius: 4px;
}

.bpmn-modeler-wrapper .node-start {
  border-left-color: #26a269;
}

.bpmn-modeler-wrapper .node-end {
  border-left-color: #d64545;
}

.bpmn-modeler-wrapper .node-gateway,
.bpmn-modeler-wrapper .node-parallel {
  border-left-color: #c78a12;
}

.bpmn-modeler-wrapper .sidebar-note {
  padding: 8px;
  margin-top: 12px;
  color: var(--workflow-muted);
  font-size: 12px;
  line-height: 18px;
  background: var(--workflow-panel-soft-bg);
  border: 1px dashed var(--workflow-border-dashed);
  border-radius: 4px;
}

.bpmn-modeler-wrapper .canvas-shell {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
  background: var(--workflow-panel-bg);
  border: 1px solid var(--workflow-border);
  border-radius: 4px;
  box-shadow: var(--workflow-shadow);
}

.bpmn-modeler-wrapper .canvas-statusbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 34px;
  padding: 0 12px;
  color: var(--workflow-muted-strong);
  font-size: 12px;
  background: var(--workflow-panel-soft-bg);
  border-bottom: 1px solid var(--workflow-border);
}

.bpmn-modeler-wrapper .bpmn-container {
  flex: 1;
  min-height: 0;
  border: none;
  background: var(--workflow-canvas-bg);
  overflow: hidden;
  --workflow-canvas-bg: #f6f9fd;
  --workflow-node-bg: #ffffff;
  --workflow-node-stroke: #7d95b8;
  --workflow-node-text: #23324d;
  --workflow-flow-stroke: #607da8;
  --workflow-label-text: #304764;
  --workflow-tool-bg: #ffffff;
  --workflow-tool-border: #cbd8e8;
  --workflow-tool-icon: #1d4f91;
}

:global(.dark) .bpmn-modeler-wrapper .bpmn-container {
  --workflow-canvas-bg: var(--el-bg-color-page);
  --workflow-node-bg: rgb(38 42 49);
  --workflow-node-stroke: rgb(116 134 162);
  --workflow-node-text: var(--el-text-color-primary);
  --workflow-flow-stroke: rgb(156 170 190);
  --workflow-label-text: var(--el-text-color-primary);
  --workflow-tool-bg: rgb(34 37 43);
  --workflow-tool-border: rgb(79 86 99);
  --workflow-tool-icon: var(--el-text-color-primary);
}

.bpmn-modeler-wrapper .properties-panel {
  width: 380px;
  flex: 0 0 380px;
  overflow-y: auto;
  background: var(--workflow-panel-bg);
  border: 1px solid var(--workflow-border);
  border-radius: 4px;
  box-shadow: var(--workflow-shadow);
}

:deep(.bpmn-container .viewport),
:deep(.bpmn-container .djs-container) {
  background-color: var(--workflow-canvas-bg);
  background-image: linear-gradient(
      var(--workflow-grid-line) 1px,
      transparent 1px
    ),
    linear-gradient(90deg, var(--workflow-grid-line) 1px, transparent 1px);
  background-size: 16px 16px;
}

:deep(.bpmn-container .djs-shape .djs-visual > rect),
:deep(.bpmn-container .djs-shape .djs-visual > circle),
:deep(.bpmn-container .djs-shape .djs-visual > ellipse),
:deep(.bpmn-container .djs-shape .djs-visual > polygon),
:deep(.bpmn-container .djs-shape .djs-visual > path) {
  fill: var(--workflow-node-bg) !important;
  stroke: var(--workflow-node-stroke) !important;
  stroke-width: 1.8px !important;
}

:deep(.bpmn-container .djs-shape .djs-visual text),
:deep(.bpmn-container .djs-label .djs-visual text) {
  fill: var(--workflow-node-text) !important;
  font-family: 'Microsoft YaHei', 'PingFang SC', Arial, sans-serif !important;
  font-size: 12px !important;
  stroke: none !important;
}

:deep(.bpmn-container .djs-connection .djs-visual > path),
:deep(.bpmn-container .djs-connection .djs-visual > polyline),
:deep(.bpmn-container .djs-connection .djs-visual marker path) {
  stroke: var(--workflow-flow-stroke) !important;
}

:deep(.bpmn-container .djs-connection .djs-visual marker path) {
  fill: var(--workflow-flow-stroke) !important;
}

:deep(.bpmn-container .djs-element.djs-label .djs-visual text) {
  fill: var(--workflow-label-text) !important;
}

:deep(.bpmn-container .djs-element[data-element-id$='_label'] .djs-visual text),
:deep(
  .bpmn-container .djs-element[data-element-id$='_label'] .djs-visual tspan
) {
  fill: var(--workflow-label-text) !important;
}

:deep(.bpmn-container .djs-palette),
:deep(.bpmn-container .djs-context-pad) {
  background: var(--workflow-tool-bg);
  border: 1px solid var(--workflow-tool-border);
  border-radius: 4px;
  box-shadow: 0 4px 14px rgb(31 63 109 / 12%);
}

:global(.dark) :deep(.bpmn-container .djs-palette),
:global(.dark) :deep(.bpmn-container .djs-context-pad) {
  box-shadow: 0 8px 20px rgb(0 0 0 / 28%);
}

:deep(.bpmn-container .djs-palette .entry),
:deep(.bpmn-container .djs-context-pad .entry) {
  color: var(--workflow-tool-icon);
  border-radius: 3px;
}

:deep(.bpmn-container .djs-palette .entry:hover),
:deep(.bpmn-container .djs-context-pad .entry:hover) {
  color: var(--workflow-primary);
  background: var(--workflow-card-icon-bg);
}

:deep(.bpmn-container .djs-context-pad.open) {
  background: var(--workflow-tool-bg);
}

:deep(.bpmn-container .djs-context-pad .entry::before),
:deep(.bpmn-container .djs-palette .entry::before) {
  color: currentColor;
}

:global(.dark) :deep(.bpmn-container .djs-context-pad .entry:hover),
:global(.dark) :deep(.bpmn-container .djs-palette .entry:hover) {
  background: rgb(46 54 66);
}

:deep(.bpmn-container .djs-element.selected .djs-outline),
:deep(.bpmn-container .djs-element.hover .djs-outline) {
  stroke: var(--workflow-primary) !important;
  stroke-width: 2px !important;
  stroke-dasharray: 4 3;
}

/* 只固定连线可视层宽度，保留 bpmn-js 较宽的命中层用于拖拽和点击 */
:deep(.bpmn-container .djs-connection .djs-visual > path),
:deep(.bpmn-container .djs-connection .djs-visual > polyline) {
  stroke-width: 2px !important;
}

/* 隐藏连线编辑手柄的可视层，保留 hit 层以维持点击与拖拽命中区域 */
:deep(.bpmn-container .djs-bendpoint .djs-visual),
:deep(.bpmn-container .djs-segment-dragger .djs-visual) {
  display: none !important;
}

:deep(.bpmn-container .djs-bendpoint .djs-hit),
:deep(.bpmn-container .djs-segment-dragger .djs-hit) {
  fill: none !important;
  stroke: transparent !important;
}

/* 审批工作流只保留业务需要的建模工具 */
:deep(.bpmn-container .djs-palette .entry),
:deep(.bpmn-container .djs-context-pad .entry) {
  display: none;
}

:deep(.bpmn-container .djs-palette .entry.bpmn-icon-hand-tool),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-lasso-tool),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-connection-multi),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-start-event-none),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-end-event-none),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-gateway-none),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-gateway-xor),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-gateway-parallel),
:deep(.bpmn-container .djs-palette .entry.bpmn-icon-user-task),
:deep(.bpmn-container .djs-context-pad .entry.bpmn-icon-end-event-none),
:deep(.bpmn-container .djs-context-pad .entry.bpmn-icon-gateway-none),
:deep(.bpmn-container .djs-context-pad .entry.bpmn-icon-user-task),
:deep(.bpmn-container .djs-context-pad .entry.bpmn-icon-connection-multi),
:deep(.bpmn-container .djs-context-pad .entry.bpmn-icon-trash) {
  display: block;
}

@media (max-width: 1200px) {
  .bpmn-modeler-wrapper .designer-sidebar {
    display: none;
  }

  .bpmn-modeler-wrapper .properties-panel {
    width: 340px;
    flex-basis: 340px;
  }
}
</style>

<style>
/* bpmn-js 的 SVG 与浮动工具条是运行时插入的 DOM，需要全局命名空间覆盖。 */
.dark .bpmn-modeler-wrapper {
  --workflow-page-bg: var(--el-bg-color-page);
  --workflow-panel-bg: var(--el-bg-color);
  --workflow-panel-soft-bg: rgb(34 37 43);
  --workflow-section-bg: rgb(32 35 40);
  --workflow-card-bg: var(--el-fill-color-light);
  --workflow-card-icon-bg: rgb(42 47 55);
  --workflow-border: rgb(64 68 77);
  --workflow-border-soft: rgb(58 62 70);
  --workflow-border-dashed: rgb(72 76 86);
  --workflow-text: var(--el-text-color-primary);
  --workflow-title: var(--el-text-color-primary);
  --workflow-muted: var(--el-text-color-secondary);
  --workflow-muted-strong: var(--el-text-color-regular);
  --workflow-primary: var(--el-color-primary);
  --workflow-grid-line: rgb(255 255 255 / 5%);
  --workflow-shadow: none;
}

.dark .bpmn-modeler-wrapper .bpmn-container {
  --workflow-canvas-bg: var(--el-bg-color-page);
  --workflow-node-bg: rgb(38 42 49);
  --workflow-node-stroke: rgb(116 134 162);
  --workflow-node-text: var(--el-text-color-primary);
  --workflow-flow-stroke: rgb(156 170 190);
  --workflow-label-text: var(--el-text-color-primary);
  --workflow-tool-bg: rgb(34 37 43);
  --workflow-tool-border: rgb(79 86 99);
  --workflow-tool-icon: var(--el-text-color-primary);
}

.bpmn-modeler-wrapper .bpmn-container .viewport,
.bpmn-modeler-wrapper .bpmn-container .djs-container,
.bpmn-modeler-wrapper .bpmn-container .djs-canvas,
.bpmn-modeler-wrapper .bpmn-container svg[data-element-id] {
  background-color: var(--workflow-canvas-bg) !important;
  background-image: linear-gradient(
      var(--workflow-grid-line) 1px,
      transparent 1px
    ),
    linear-gradient(90deg, var(--workflow-grid-line) 1px, transparent 1px) !important;
  background-size: 16px 16px !important;
}

.dark .bpmn-modeler-wrapper .bpmn-container svg,
.dark .bpmn-modeler-wrapper .bpmn-container .viewport,
.dark .bpmn-modeler-wrapper .bpmn-container .djs-container,
.dark .bpmn-modeler-wrapper .bpmn-container .djs-canvas,
.dark .bpmn-modeler-wrapper .bpmn-container .djs-dragger {
  background-color: var(--workflow-canvas-bg) !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-shape .djs-visual > rect,
.bpmn-modeler-wrapper .bpmn-container .djs-shape .djs-visual > circle,
.bpmn-modeler-wrapper .bpmn-container .djs-shape .djs-visual > ellipse,
.bpmn-modeler-wrapper .bpmn-container .djs-shape .djs-visual > polygon,
.bpmn-modeler-wrapper .bpmn-container .djs-shape .djs-visual > path,
.bpmn-modeler-wrapper .bpmn-container .djs-dragger .djs-visual > rect,
.bpmn-modeler-wrapper .bpmn-container .djs-dragger .djs-visual > circle,
.bpmn-modeler-wrapper .bpmn-container .djs-dragger .djs-visual > ellipse,
.bpmn-modeler-wrapper .bpmn-container .djs-dragger .djs-visual > polygon,
.bpmn-modeler-wrapper .bpmn-container .djs-dragger .djs-visual > path {
  fill: var(--workflow-node-bg) !important;
  stroke: var(--workflow-node-stroke) !important;
  stroke-width: 1.8px !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-shape .djs-visual text,
.bpmn-modeler-wrapper .bpmn-container .djs-label .djs-visual text,
.bpmn-modeler-wrapper .bpmn-container .djs-label .djs-visual tspan {
  fill: var(--workflow-node-text) !important;
  font-family: 'Microsoft YaHei', 'PingFang SC', Arial, sans-serif !important;
  font-size: 12px !important;
  stroke: none !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-connection .djs-visual > path,
.bpmn-modeler-wrapper .bpmn-container .djs-connection .djs-visual > polyline,
.bpmn-modeler-wrapper .bpmn-container .djs-connection .djs-visual marker path {
  stroke: var(--workflow-flow-stroke) !important;
  stroke-width: 2px !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-connection .djs-visual marker path {
  fill: var(--workflow-flow-stroke) !important;
}

.bpmn-modeler-wrapper
  .bpmn-container
  .djs-connection.selected
  .djs-visual
  > path {
  stroke: var(--workflow-primary) !important;
  stroke-width: 3px !important;
}

.bpmn-modeler-wrapper
  .bpmn-container
  .djs-element[data-element-id$='_label']
  .djs-visual
  text,
.bpmn-modeler-wrapper
  .bpmn-container
  .djs-element[data-element-id$='_label']
  .djs-visual
  tspan {
  fill: var(--workflow-label-text) !important;
  stroke: var(--workflow-canvas-bg) !important;
  stroke-width: 5px !important;
  stroke-linejoin: round !important;
  paint-order: stroke !important;
  font-weight: 600 !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-palette,
.bpmn-modeler-wrapper .bpmn-container .djs-context-pad {
  background: var(--workflow-tool-bg) !important;
  border: 1px solid var(--workflow-tool-border) !important;
  border-radius: 4px !important;
}

.dark .bpmn-modeler-wrapper .bpmn-container .djs-palette,
.dark .bpmn-modeler-wrapper .bpmn-container .djs-context-pad {
  box-shadow: 0 8px 20px rgb(0 0 0 / 28%) !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-palette .entry,
.bpmn-modeler-wrapper .bpmn-container .djs-context-pad .entry {
  color: var(--workflow-tool-icon) !important;
  background-color: transparent !important;
  border: 1px solid transparent !important;
  border-radius: 3px;
  box-shadow: none !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-palette .entry:hover,
.bpmn-modeler-wrapper .bpmn-container .djs-context-pad .entry:hover {
  color: var(--workflow-primary) !important;
  background: var(--workflow-card-icon-bg) !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-context-pad .group {
  background: var(--workflow-tool-bg) !important;
}

.bpmn-modeler-wrapper .bpmn-container .djs-context-pad .entry::before,
.bpmn-modeler-wrapper .bpmn-container .djs-palette .entry::before {
  color: currentColor !important;
}

.dark .bpmn-modeler-wrapper .bpmn-container .djs-context-pad .entry {
  background-color: rgb(34 37 43) !important;
  border-color: rgb(79 86 99) !important;
}

.dark .bpmn-modeler-wrapper .bpmn-container .djs-context-pad .entry:hover {
  color: var(--workflow-primary) !important;
  background-color: rgb(46 54 66) !important;
}
</style>
