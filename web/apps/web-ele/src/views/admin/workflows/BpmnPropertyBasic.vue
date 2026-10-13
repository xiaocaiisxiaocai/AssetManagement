<script lang="ts" setup>
import { ElFormItem, ElInput } from 'element-plus';

defineProps<{
  elementId: string;
  elementTypeLabel: string;
}>();

const elementName = defineModel<string>('elementName', { required: true });
</script>

<template>
  <section class="property-section">
    <div class="section-title">基础信息</div>

    <ElFormItem label="节点类型">
      <div class="readonly-field">
        {{ elementTypeLabel }}
      </div>
    </ElFormItem>

    <ElFormItem label="技术标识">
      <div class="readonly-field code-field">{{ elementId }}</div>
    </ElFormItem>

    <ElFormItem label="显示名称">
      <ElInput v-model="elementName" placeholder="请输入节点名称" />
    </ElFormItem>
  </section>
</template>

<style scoped src="./bpmn-properties.css"></style>
