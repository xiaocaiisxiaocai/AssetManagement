<script lang="ts" setup>
import { ElFormItem, ElInput, ElOption, ElSelect } from 'element-plus';

defineProps<{
  conditionExpressionPlaceholder: string;
  conditionFields: { label: string; value: string }[];
  conditionOperators: { label: string; value: string }[];
  conditionValueOptions: { label: string; value: string }[];
  conditionValuePlaceholder: string;
}>();

const conditionField = defineModel<string>('conditionField', { required: true });
const conditionOperator = defineModel<string>('conditionOperator', {
  required: true,
});
const conditionValue = defineModel<string>('conditionValue', { required: true });
const conditionExpression = defineModel<string>('conditionExpression', {
  required: true,
});
</script>

<template>
  <section class="property-section">
    <div class="section-title">流转条件</div>

    <ElFormItem label="条件">
      <div class="condition-builder">
        <ElSelect v-model="conditionField" style="width: 132px">
          <ElOption
            v-for="item in conditionFields"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </ElSelect>
        <ElSelect v-model="conditionOperator" style="width: 82px">
          <ElOption
            v-for="item in conditionOperators"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </ElSelect>
        <ElSelect
          v-model="conditionValue"
          :placeholder="conditionValuePlaceholder"
          clearable
          filterable
          style="width: 100%"
        >
          <ElOption
            v-for="item in conditionValueOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </ElSelect>
      </div>
      <ElInput
        v-model="conditionExpression"
        :placeholder="conditionExpressionPlaceholder"
        :rows="3"
        class="expression-input"
        type="textarea"
      />
    </ElFormItem>

    <div class="tip-box">
      条件为空时表示默认流向；支持申请部门、申请人角色和组织审批条件。
    </div>
  </section>
</template>

<style scoped src="./bpmn-properties.css"></style>
