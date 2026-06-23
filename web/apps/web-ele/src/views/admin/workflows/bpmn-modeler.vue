<script lang="ts" setup>
import { onMounted, onUnmounted, ref, shallowRef } from 'vue';
import { ElButton, ElMessage } from 'element-plus';
import BpmnModeler from 'bpmn-js/lib/Modeler';
import 'bpmn-js/dist/assets/bpmn-font/css/bpmn-embedded.css';
import 'bpmn-js/dist/assets/bpmn-js.css';
import 'bpmn-js/dist/assets/diagram-js.css';
import BpmnProperties from './bpmn-properties.vue';

defineOptions({ name: 'BpmnModeler' });

interface Props {
  workflowId: number;
  initialXml?: string;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  save: [bpmnXml: string];
}>();

const loading = ref(false);
const saving = ref(false);
const containerRef = ref<HTMLDivElement>();
const modeler = shallowRef<any>();
const selectedElement = shallowRef<any>(null); // 当前选中的元素

const allowedPaletteEntries = new Set([
  'hand-tool',
  'global-connect-tool',
  'tool-separator',
  'create.start-event',
  'create.end-event',
  'create.exclusive-gateway',
  'create.parallel-gateway',
  'create.task',
]);

const allowedContextPadEntries = new Set([
  'append.end-event',
  'append.gateway',
  'append.append-task',
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

        return nextEntries;
      };
    },
  });

  function createShapeEntry(type: string, group: string, className: string, title: string) {
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

    // 自动适配画布大小
    const canvas = modeler.value.get('canvas');
    canvas.zoom('fit-viewport');

    // 监听元素选中事件
    const eventBus = modeler.value.get('eventBus');
    eventBus.on('selection.changed', (event: any) => {
      const { newSelection } = event;
      if (newSelection && newSelection.length > 0) {
        selectedElement.value = newSelection[0];
      } else {
        selectedElement.value = null;
      }
    });

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

onMounted(() => {
  initModeler();
});

onUnmounted(() => {
  if (import.meta.env.DEV) {
    delete (window as any).__bpmnModeler;
  }
});
</script>

<template>
  <div class="bpmn-modeler-wrapper">
    <div class="toolbar">
      <div class="toolbar-left">
        <ElButton type="primary" :loading="saving" @click="handleSave">
          保存
        </ElButton>
        <ElButton @click="handleDownload">
          下载 BPMN
        </ElButton>
      </div>
      <div class="toolbar-right">
        <ElButton @click="handleZoomIn">放大</ElButton>
        <ElButton @click="handleZoomOut">缩小</ElButton>
        <ElButton @click="handleZoomReset">适应画布</ElButton>
      </div>
    </div>

    <div class="modeler-content">
      <div
        ref="containerRef"
        v-loading="loading"
        class="bpmn-container"
        element-loading-text="加载中..."
      />

      <!-- 属性面板 -->
      <div class="properties-panel">
        <BpmnProperties :element="selectedElement" :modeler="modeler" />
      </div>
    </div>

    <div class="help-text">
      <p><strong>使用说明：</strong></p>
      <ul>
        <li>从左侧面板拖拽元素到画布</li>
        <li>点击元素后，在右侧属性面板配置属性</li>
        <li>UserTask 用于审批节点，配置审批人类型</li>
        <li>ExclusiveGateway（排他网关）用于条件分支，在连线上配置条件表达式</li>
        <li>ParallelGateway（并行网关）用于并行审批</li>
      </ul>
    </div>
  </div>
</template>

<style>
/* 全局样式，不使用 scoped，确保能覆盖 bpmn-js */
.bpmn-modeler-wrapper {
  display: flex;
  flex-direction: column;
  height: 70vh;
}

.bpmn-modeler-wrapper .toolbar {
  display: flex;
  justify-content: space-between;
  padding: 10px;
  background: #f5f5f5;
  border-bottom: 1px solid #ddd;
}

.bpmn-modeler-wrapper .toolbar-left,
.bpmn-modeler-wrapper .toolbar-right {
  display: flex;
  gap: 10px;
}

.bpmn-modeler-wrapper .modeler-content {
  flex: 1;
  display: flex;
  overflow: hidden;
}

.bpmn-modeler-wrapper .bpmn-container {
  flex: 1;
  border: 1px solid #ddd;
  background: #fff;
  overflow: hidden;
}

.bpmn-modeler-wrapper .properties-panel {
  width: 300px;
  overflow-y: auto;
}

.bpmn-modeler-wrapper .help-text {
  padding: 10px;
  background: #f9f9f9;
  border-top: 1px solid #ddd;
  font-size: 12px;
  color: #666;
}

.bpmn-modeler-wrapper .help-text ul {
  margin: 5px 0 0 20px;
  padding: 0;
}

.bpmn-modeler-wrapper .help-text li {
  margin: 3px 0;
}

/* 只固定连线可视层宽度，保留 bpmn-js 较宽的命中层用于拖拽和点击 */
.bpmn-container .djs-connection .djs-visual > path,
.bpmn-container .djs-connection .djs-visual > polyline {
  stroke-width: 2px !important;
}

/* 隐藏连线编辑手柄的可视层，保留 hit 层以维持点击与拖拽命中区域 */
.bpmn-container .djs-bendpoint .djs-visual,
.bpmn-container .djs-segment-dragger .djs-visual {
  display: none !important;
}

.bpmn-container .djs-bendpoint .djs-hit,
.bpmn-container .djs-segment-dragger .djs-hit {
  fill: none !important;
  stroke: transparent !important;
}

/* 审批工作流只保留业务需要的建模工具 */
.bpmn-container .djs-palette .entry,
.bpmn-container .djs-context-pad .entry {
  display: none;
}

.bpmn-container .djs-palette .entry.bpmn-icon-hand-tool,
.bpmn-container .djs-palette .entry.bpmn-icon-connection-multi,
.bpmn-container .djs-palette .entry.bpmn-icon-start-event-none,
.bpmn-container .djs-palette .entry.bpmn-icon-end-event-none,
.bpmn-container .djs-palette .entry.bpmn-icon-gateway-none,
.bpmn-container .djs-palette .entry.bpmn-icon-gateway-xor,
.bpmn-container .djs-palette .entry.bpmn-icon-gateway-parallel,
.bpmn-container .djs-palette .entry.bpmn-icon-user-task,
.bpmn-container .djs-context-pad .entry.bpmn-icon-end-event-none,
.bpmn-container .djs-context-pad .entry.bpmn-icon-gateway-none,
.bpmn-container .djs-context-pad .entry.bpmn-icon-user-task,
.bpmn-container .djs-context-pad .entry.bpmn-icon-connection-multi,
.bpmn-container .djs-context-pad .entry.bpmn-icon-trash {
  display: block;
}
</style>
