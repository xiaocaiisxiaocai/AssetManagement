import type { ElementEditorState } from './bpmn-element-update';

import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

import { runHandled } from '#/utils/handled-promise';

import {
  assigneeTypeOptions,
  buildConditionFields,
  useAssigneeOptions,
} from './bpmn-assignee-options';
import {
  buildConditionExpression as composeConditionExpression,
  conditionValuePlaceholder,
  getConditionValueOptions as resolveConditionValueOptions,
} from './bpmn-condition';
import {
  applyElementUpdate,
  assignLoadedElement,
  elementTypeLabel,
  useElementKind,
} from './bpmn-element-update';
import {
  focusGatewayBranch as focusGatewayFlow,
  type GatewayCondition,
  readGatewayConditions,
  writeGatewayCondition,
} from './bpmn-gateway-conditions';

interface BpmnPropertiesProps {
  element: any;
  modeler: any;
}

export function useBpmnProperties(props: BpmnPropertiesProps) {
  const elementType = ref('');
  const elementName = ref('');
  const elementId = ref('');
  const assignee = useAssigneeOptions();
  const kind = useElementKind(elementType);
  const conditionExpression = ref('');
  const conditionExpressionPlaceholder = [
    '如: $',
    '{applicantDept} == "信息部"',
  ].join('');
  const conditionField = ref('applicantDept');
  const conditionOperator = ref('==');
  const conditionValue = ref('');
  const gatewayConditions = ref<GatewayCondition[]>([]);
  const isLoadingElement = ref(false);
  let boundEventBus: any;
  let gatewayRefreshQueued = false;
  const loadedElementState: ElementEditorState = {
    approvalMode: assignee.approvalMode,
    assigneeType: assignee.assigneeType,
    assigneeValue: assignee.assigneeValue,
    conditionExpression,
    conditionField,
    conditionOperator,
    conditionValue,
    elementId,
    elementName,
    elementType,
  };

  const conditionFields = computed(() =>
    buildConditionFields(assignee.organizationLevels.value),
  );
  const conditionOperators = computed(() => [{ label: '等于', value: '==' }]);
  const conditionValueOptions = computed(() =>
    getConditionValueOptions(conditionField.value),
  );
  const defaultBranchCount = computed(
    () => gatewayConditions.value.filter((item) => item.isDefault).length,
  );

  function getConditionValueOptions(field: string) {
    return resolveConditionValueOptions(field, {
      departments: assignee.departmentOptions.value,
      roles: assignee.roleOptions.value,
    });
  }

  function buildConditionExpression() {
    conditionExpression.value = composeConditionExpression(
      conditionValue.value,
      conditionField.value,
      conditionOperator.value,
    );
  }

  function loadElement() {
    isLoadingElement.value = true;
    const loaded = assignLoadedElement(props.element, loadedElementState);
    gatewayConditions.value =
      loaded && kind.isGateway.value
        ? readGatewayConditions(props.element, elementType.value)
        : [];
    nextTick(() => {
      isLoadingElement.value = false;
    });
  }

  function updateElement() {
    if (isLoadingElement.value || !props.element || !props.modeler) return;
    applyElementUpdate(props.modeler, props.element, {
      approvalMode: assignee.approvalMode.value,
      assigneeType: assignee.assigneeType.value,
      assigneeValue: assignee.assigneeValue.value,
      conditionExpression: conditionExpression.value,
      elementName: elementName.value,
      isSequenceFlow: kind.isSequenceFlow.value,
      isUserTask: kind.isUserTask.value,
    });
  }

  function updateGatewayCondition(item: GatewayCondition) {
    if (isLoadingElement.value || !props.modeler) return;
    writeGatewayCondition(props.modeler, elementType.value, item);
  }

  function focusGatewayBranch(item: GatewayCondition) {
    if (!props.modeler) return;
    focusGatewayFlow(props.modeler, item);
  }

  function handleCommandStackChanged() {
    if (!kind.isGateway.value || gatewayRefreshQueued) return;

    gatewayRefreshQueued = true;
    queueMicrotask(() => {
      gatewayRefreshQueued = false;
      if (!kind.isGateway.value) return;
      gatewayConditions.value = readGatewayConditions(
        props.element,
        elementType.value,
      );
    });
  }

  function bindModelerEvents(nextModeler: any, previousModeler?: any) {
    previousModeler
      ?.get?.('eventBus')
      ?.off?.('commandStack.changed', handleCommandStackChanged);
    boundEventBus = nextModeler?.get?.('eventBus');
    boundEventBus?.on('commandStack.changed', handleCommandStackChanged);
  }

  watch(() => props.element, loadElement, { immediate: true });
  watch(() => props.modeler, bindModelerEvents, { immediate: true });
  watch(
    [
      elementName,
      assignee.assigneeType,
      assignee.assigneeValue,
      assignee.approvalMode,
      conditionExpression,
    ],
    updateElement,
  );
  watch(assignee.assigneeType, (newType, oldType) => {
    if (isLoadingElement.value || newType === oldType) return;
    assignee.assigneeValue.value = newType === 'usernames' ? [] : '';
    if (newType !== 'usernames' && assignee.approvalMode.value === 'all') {
      assignee.approvalMode.value = 'any';
    }
  });
  watch(conditionField, () => {
    if (isLoadingElement.value) return;
    conditionOperator.value = '==';
    buildConditionExpression();
  });
  watch([conditionOperator, conditionValue], () => {
    if (isLoadingElement.value) return;
    buildConditionExpression();
  });

  onMounted(() => {
    runHandled(assignee.loadAssigneeOptions());
  });

  onUnmounted(() => {
    boundEventBus?.off?.('commandStack.changed', handleCommandStackChanged);
  });

  return {
    approvalMode: assignee.approvalMode,
    assigneeOptionsLoading: assignee.assigneeOptionsLoading,
    assigneeType: assignee.assigneeType,
    assigneeTypes: assigneeTypeOptions,
    assigneeValue: assignee.assigneeValue,
    assigneeValueOptions: assignee.assigneeValueOptions,
    assigneeValuePlaceholder: assignee.assigneeValuePlaceholder,
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
    isConditionGateway: kind.isConditionGateway,
    isGateway: kind.isGateway,
    isSequenceFlow: kind.isSequenceFlow,
    isUserTask: kind.isUserTask,
    loadAssigneeOptions: assignee.loadAssigneeOptions,
    updateGatewayCondition,
  };
}
