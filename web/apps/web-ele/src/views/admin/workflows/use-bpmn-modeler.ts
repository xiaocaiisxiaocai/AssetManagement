import { computed, onMounted, onUnmounted, ref, shallowRef } from 'vue';

import BpmnModeler from 'bpmn-js/lib/Modeler';
import { ElMessage } from 'element-plus';

import { createGatewayBranchTools } from './bpmn-modeler-branches';
import {
  beginCreateShape,
  changeZoom,
  describeSelectedElement,
  fitViewport,
  placeShapeOnCanvas,
} from './bpmn-modeler-canvas';
import { installWorkflowPalette } from './bpmn-modeler-palette';
import {
  downloadWorkflowXml,
  emptyBpmnTemplate,
  exportFormattedXml,
} from './bpmn-modeler-xml';
import { resolveDiagramElement } from './gateway-branches';

interface BpmnModelerProps {
  initialXml?: string;
  saving?: boolean;
  workflowId: number;
}

interface BpmnModelerEmit {
  (event: 'exportingChange', exporting: boolean): void;
  (event: 'save', bpmnXml: string, onComplete: () => void): void;
}

interface BpmnCanvasExpose {
  containerRef?: HTMLDivElement;
}

export function useBpmnModeler(
  props: BpmnModelerProps,
  emit: BpmnModelerEmit,
  canvasRef: { value?: BpmnCanvasExpose },
) {
  const loading = ref(false);
  const exporting = ref(false);
  const savePending = computed(() => exporting.value || Boolean(props.saving));
  const modeler = shallowRef<any>();
  const selectedElement = shallowRef<any>(null);
  const selectedElementText = computed(() =>
    describeSelectedElement(selectedElement.value),
  );
  const branches = createGatewayBranchTools(modeler);

  function setExporting(value: boolean) {
    exporting.value = value;
    emit('exportingChange', value);
  }

  async function initModeler() {
    // expose 的 containerRef 在父脚本里已自动解包，不要再读 .value。
    const container = canvasRef.value?.containerRef;
    if (!container) return;

    loading.value = true;
    try {
      modeler.value = new BpmnModeler({
        container,
        keyboard: { bindTo: document },
      });
      installWorkflowPalette(modeler.value);
      if (import.meta.env.DEV) {
        (window as any).__bpmnModeler = modeler.value;
      }

      const xml = props.initialXml || emptyBpmnTemplate;
      await modeler.value.importXML(xml);
      const normalizedBranchCount = branches.normalizeGatewayBranchLabels();
      if (normalizedBranchCount > 0) {
        ElMessage.info(
          `已为 ${normalizedBranchCount} 条旧分支补充画布标签，保存后将写入流程定义`,
        );
      }

      fitViewport(modeler.value);
      const eventBus = modeler.value.get('eventBus');
      eventBus.on('selection.changed', (event: any) => {
        const { newSelection } = event;
        selectedElement.value =
          newSelection && newSelection.length > 0
            ? resolveDiagramElement(newSelection[0])
            : null;
      });
      eventBus.on(
        'commandStack.changed',
        branches.scheduleGatewayBranchNormalization,
      );
    } catch (error: any) {
      console.error('初始化 BPMN 设计器失败:', error);
      ElMessage.error(error.message || '初始化失败');
    } finally {
      loading.value = false;
    }
  }

  async function handleSave() {
    if (!modeler.value || savePending.value) return;

    setExporting(true);
    let handedOff = false;
    try {
      branches.normalizeGatewayBranchLabels();
      if (!branches.validateGatewayBranches()) return;
      const xml = await exportFormattedXml(modeler.value);
      handedOff = true;
      emit('save', xml, () => {
        setExporting(false);
      });
    } catch (error: any) {
      console.error('保存 BPMN XML 失败:', error);
      ElMessage.error(error.message || '保存失败');
    } finally {
      if (!handedOff) setExporting(false);
    }
  }

  async function handleDownload() {
    if (!modeler.value) return;

    try {
      const xml = await exportFormattedXml(modeler.value);
      downloadWorkflowXml(xml, props.workflowId);
      ElMessage.success('下载成功');
    } catch (error: any) {
      console.error('下载失败:', error);
      ElMessage.error(error.message || '下载失败');
    }
  }

  async function handleZoomIn() {
    changeZoom(modeler.value, 0.1);
  }

  async function handleZoomOut() {
    changeZoom(modeler.value, -0.1);
  }

  async function handleZoomReset() {
    fitViewport(modeler.value);
  }

  function startCreateShape(event: DragEvent | MouseEvent, type: string) {
    if (!modeler.value) return;
    beginCreateShape(modeler.value, event, type);
  }

  function handleNodeCardKeydown(event: KeyboardEvent, type: string) {
    if (event.key !== 'Enter' && event.key !== ' ') return;

    event.preventDefault();
    if (!modeler.value) return;
    placeShapeOnCanvas(modeler.value, type);
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

  return {
    handleDownload,
    handleNodeCardKeydown,
    handleSave,
    handleZoomIn,
    handleZoomOut,
    handleZoomReset,
    loading,
    modeler,
    savePending,
    selectedElement,
    selectedElementText,
    startCreateShape,
  };
}
