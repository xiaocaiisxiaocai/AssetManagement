<script lang="ts" setup>
import { ref } from 'vue';

import BpmnProperties from './bpmn-properties.vue';
import BpmnDesignerCanvas from './BpmnDesignerCanvas.vue';
import BpmnDesignerSidebar from './BpmnDesignerSidebar.vue';
import BpmnDesignerToolbar from './BpmnDesignerToolbar.vue';
import { useBpmnModeler } from './use-bpmn-modeler';

import 'bpmn-js/dist/assets/bpmn-font/css/bpmn-embedded.css';
import 'bpmn-js/dist/assets/bpmn-js.css';
import 'bpmn-js/dist/assets/diagram-js.css';

interface Props {
  workflowId: number;
  initialXml?: string;
  saving?: boolean;
}

defineOptions({ name: 'BpmnModeler' });

const props = defineProps<Props>();

const emit = defineEmits<{
  exportingChange: [exporting: boolean];
  save: [bpmnXml: string, onComplete: () => void];
}>();

const canvasRef = ref<InstanceType<typeof BpmnDesignerCanvas>>();

const {
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
} = useBpmnModeler(props, emit, canvasRef);
</script>

<template>
  <div class="bpmn-modeler-wrapper">
    <BpmnDesignerToolbar
      :loading="loading"
      :save-pending="savePending"
      @download="handleDownload"
      @save="handleSave"
      @zoom-in="handleZoomIn"
      @zoom-out="handleZoomOut"
      @zoom-reset="handleZoomReset"
    />

    <div class="designer-content">
      <BpmnDesignerSidebar
        @create-keydown="handleNodeCardKeydown"
        @create-start="startCreateShape"
      />
      <BpmnDesignerCanvas
        ref="canvasRef"
        :loading="loading"
        :selected-element-text="selectedElementText"
      />

      <div class="properties-panel">
        <BpmnProperties :element="selectedElement" :modeler="modeler" />
      </div>
    </div>
  </div>
</template>

<style src="./bpmn-modeler.css"></style>
