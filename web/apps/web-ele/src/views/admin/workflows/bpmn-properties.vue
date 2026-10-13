<script lang="ts" setup>
import { ElForm } from 'element-plus';

import BpmnPropertyAssignee from './BpmnPropertyAssignee.vue';
import BpmnPropertyBasic from './BpmnPropertyBasic.vue';
import BpmnPropertyCondition from './BpmnPropertyCondition.vue';
import BpmnPropertyGateway from './BpmnPropertyGateway.vue';
import { useBpmnProperties } from './use-bpmn-properties';

interface Props {
  element: any; // BPMN 元素
  modeler: any; // BPMN Modeler 实例
}

defineOptions({ name: 'BpmnProperties' });

const props = defineProps<Props>();

const {
  approvalMode,
  assigneeOptionsLoading,
  assigneeType,
  assigneeTypes,
  assigneeValue,
  assigneeValueOptions,
  assigneeValuePlaceholder,
  conditionExpression,
  conditionExpressionPlaceholder,
  conditionField,
  conditionFields,
  conditionOperator,
  conditionOperators,
  conditionValue,
  conditionValueOptions,
  conditionValuePlaceholder,
  defaultBranchCount,
  elementId,
  elementName,
  elementType,
  elementTypeLabel,
  focusGatewayBranch,
  gatewayConditions,
  getConditionValueOptions,
  isConditionGateway,
  isGateway,
  isSequenceFlow,
  isUserTask,
  loadAssigneeOptions,
  updateGatewayCondition,
} = useBpmnProperties(props);
</script>

<template>
  <div class="bpmn-properties">
    <div class="panel-header">
      <div>
        <div class="panel-title">属性配置</div>
        <div class="panel-subtitle">按业务语言维护流程规则</div>
      </div>
    </div>

    <div v-if="!element" class="empty-state">
      <div class="empty-box">
        <div class="empty-title">未选择元素</div>
        <div class="empty-desc">
          点击画布上的节点或连线后，在这里配置名称、审批人和分支条件。
        </div>
      </div>
    </div>

    <div v-else class="properties-form">
      <ElForm label-position="top" label-width="100px" size="small">
        <BpmnPropertyBasic
          v-model:element-name="elementName"
          :element-id="elementId"
          :element-type-label="elementTypeLabel(elementType)"
        />
        <BpmnPropertyAssignee
          v-if="isUserTask"
          v-model:approval-mode="approvalMode"
          v-model:assignee-type="assigneeType"
          v-model:assignee-value="assigneeValue"
          :assignee-options-loading="assigneeOptionsLoading"
          :assignee-type-options="assigneeTypes"
          :assignee-value-options="assigneeValueOptions"
          :assignee-value-placeholder="assigneeValuePlaceholder"
          @load-options="loadAssigneeOptions"
        />
        <BpmnPropertyCondition
          v-if="isSequenceFlow"
          v-model:condition-expression="conditionExpression"
          v-model:condition-field="conditionField"
          v-model:condition-operator="conditionOperator"
          v-model:condition-value="conditionValue"
          :condition-expression-placeholder="conditionExpressionPlaceholder"
          :condition-fields="conditionFields"
          :condition-operators="conditionOperators"
          :condition-value-options="conditionValueOptions"
          :condition-value-placeholder="
            conditionValuePlaceholder(conditionField)
          "
        />
        <BpmnPropertyGateway
          v-if="isGateway"
          :condition-fields="conditionFields"
          :condition-value-placeholder="conditionValuePlaceholder"
          :default-branch-count="defaultBranchCount"
          :element-name="elementName"
          :element-type="elementType"
          :gateway-conditions="gatewayConditions"
          :get-condition-value-options="getConditionValueOptions"
          :is-condition-gateway="isConditionGateway"
          @focus-branch="focusGatewayBranch"
          @update-condition="updateGatewayCondition"
        />
      </ElForm>
    </div>
  </div>
</template>

<style scoped src="./bpmn-properties.css"></style>
