<script lang="ts" setup>
const emit = defineEmits<{
  createKeydown: [event: KeyboardEvent, type: string];
  createStart: [event: DragEvent | MouseEvent, type: string];
}>();
</script>

<template>
  <aside class="designer-sidebar">
    <div class="sidebar-title">组件库</div>
    <div
      aria-label="添加发起节点到画布"
      class="node-card node-start"
      draggable="true"
      role="button"
      tabindex="0"
      @dragstart="emit('createStart', $event, 'bpmn:StartEvent')"
      @keydown="emit('createKeydown', $event, 'bpmn:StartEvent')"
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
      @dragstart="emit('createStart', $event, 'bpmn:UserTask')"
      @keydown="emit('createKeydown', $event, 'bpmn:UserTask')"
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
      @dragstart="emit('createStart', $event, 'bpmn:ExclusiveGateway')"
      @keydown="emit('createKeydown', $event, 'bpmn:ExclusiveGateway')"
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
      @dragstart="emit('createStart', $event, 'bpmn:ParallelGateway')"
      @keydown="emit('createKeydown', $event, 'bpmn:ParallelGateway')"
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
      @dragstart="emit('createStart', $event, 'bpmn:InclusiveGateway')"
      @keydown="emit('createKeydown', $event, 'bpmn:InclusiveGateway')"
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
      @dragstart="emit('createStart', $event, 'bpmn:ServiceTask')"
      @keydown="emit('createKeydown', $event, 'bpmn:ServiceTask')"
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
      @dragstart="emit('createStart', $event, 'bpmn:EndEvent')"
      @keydown="emit('createKeydown', $event, 'bpmn:EndEvent')"
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
</template>
