import type { AssigneeType } from './assignee-identities';

import { computed, type Ref } from 'vue';

import {
  loadAssigneeSelection,
  serializeAssigneeSelection,
} from './assignee-identities';
import { parseConditionExpression } from './bpmn-condition';
import { gatewaySupportsConditions } from './gateway-branches';

interface WritableValue<T> {
  value: T;
}

export interface ElementEditorState {
  approvalMode: WritableValue<'all' | 'any'>;
  assigneeType: WritableValue<AssigneeType>;
  assigneeValue: WritableValue<string | string[]>;
  conditionExpression: WritableValue<string>;
  conditionField: WritableValue<string>;
  conditionOperator: WritableValue<string>;
  conditionValue: WritableValue<string>;
  elementId: WritableValue<string>;
  elementName: WritableValue<string>;
  elementType: WritableValue<string>;
}

interface ElementUpdateInput {
  approvalMode: 'all' | 'any';
  assigneeType: AssigneeType;
  assigneeValue: string | string[];
  conditionExpression: string;
  elementName: string;
  isSequenceFlow: boolean;
  isUserTask: boolean;
}

interface ElementPropertyUpdate {
  'camunda:approvalMode'?: string;
  'camunda:assignee'?: string;
  'camunda:candidateGroups'?: string;
  'camunda:candidateUsers'?: string;
  conditionExpression?: unknown;
  name?: string;
}

const elementTypeNames: { [type: string]: string } = {
  'bpmn:EndEvent': '结束节点',
  'bpmn:ExclusiveGateway': '条件分支',
  'bpmn:InclusiveGateway': '包容分支',
  'bpmn:ParallelGateway': '并行审批',
  'bpmn:SequenceFlow': '流转线',
  'bpmn:StartEvent': '发起节点',
  'bpmn:UserTask': '审批节点',
};

export function elementTypeLabel(type: string) {
  return elementTypeNames[type] || type || '-';
}

export function useElementKind(elementType: Ref<string>) {
  const isUserTask = computed(() => elementType.value === 'bpmn:UserTask');
  const isSequenceFlow = computed(
    () => elementType.value === 'bpmn:SequenceFlow',
  );
  const isGateway = computed(
    () =>
      elementType.value === 'bpmn:ExclusiveGateway' ||
      elementType.value === 'bpmn:ParallelGateway' ||
      elementType.value === 'bpmn:InclusiveGateway',
  );
  const isConditionGateway = computed(() =>
    gatewaySupportsConditions(elementType.value),
  );

  return { isConditionGateway, isGateway, isSequenceFlow, isUserTask };
}

export function assignLoadedElement(element: any, state: ElementEditorState) {
  if (!element) {
    state.elementType.value = '';
    state.elementName.value = '';
    state.elementId.value = '';
    state.assigneeType.value = '';
    state.assigneeValue.value = '';
    state.approvalMode.value = 'any';
    state.conditionExpression.value = '';
    state.conditionField.value = 'applicantDept';
    state.conditionOperator.value = '==';
    state.conditionValue.value = '';
    return false;
  }

  const businessObject = element.businessObject;
  state.elementType.value = businessObject.$type;
  state.elementName.value = businessObject.name || '';
  state.elementId.value = businessObject.id || '';

  if (state.elementType.value === 'bpmn:UserTask') {
    const assignee = businessObject.get('camunda:assignee');
    const candidateUsers = businessObject.get('camunda:candidateUsers');
    const candidateGroups = businessObject.get('camunda:candidateGroups');
    state.approvalMode.value =
      businessObject.get('camunda:approvalMode') === 'all' ? 'all' : 'any';
    const selection = loadAssigneeSelection(
      assignee,
      candidateUsers,
      candidateGroups,
    );
    state.assigneeType.value = selection.type;
    state.assigneeValue.value = selection.value;
  } else {
    state.assigneeType.value = '';
    state.assigneeValue.value = '';
    state.approvalMode.value = 'any';
  }

  if (state.elementType.value === 'bpmn:SequenceFlow') {
    const conditionExpr = businessObject.conditionExpression;
    if (conditionExpr) {
      state.conditionExpression.value = conditionExpr.body || '';
      const parsed = parseConditionExpression(state.conditionExpression.value);
      state.conditionField.value = parsed.field;
      state.conditionOperator.value = parsed.operator;
      state.conditionValue.value = parsed.value;
    } else {
      state.conditionExpression.value = '';
      state.conditionField.value = 'applicantDept';
      state.conditionOperator.value = '==';
      state.conditionValue.value = '';
    }
  } else {
    state.conditionExpression.value = '';
    state.conditionValue.value = '';
  }

  return true;
}

export function applyElementUpdate(
  modeler: any,
  element: any,
  input: ElementUpdateInput,
) {
  const modeling = modeler.get('modeling');
  const businessObject = element.businessObject;
  const updates: ElementPropertyUpdate = {};

  if (input.elementName !== businessObject.name) {
    updates.name = input.elementName;
  }

  if (input.isUserTask) {
    const {
      assignee: assigneeVal,
      candidateGroups: candidateGroupsVal,
      candidateUsers: candidateUsersVal,
    } = serializeAssigneeSelection(input.assigneeType, input.assigneeValue);

    if ((businessObject.get('camunda:assignee') || '') !== assigneeVal) {
      updates['camunda:assignee'] = assigneeVal || undefined;
    }

    if (
      (businessObject.get('camunda:candidateUsers') || '') !== candidateUsersVal
    ) {
      updates['camunda:candidateUsers'] = candidateUsersVal || undefined;
    }

    if (
      (businessObject.get('camunda:candidateGroups') || '') !==
      candidateGroupsVal
    ) {
      updates['camunda:candidateGroups'] = candidateGroupsVal || undefined;
    }

    const approvalModeVal = input.approvalMode === 'all' ? 'all' : '';
    if (
      (businessObject.get('camunda:approvalMode') || '') !== approvalModeVal
    ) {
      updates['camunda:approvalMode'] = approvalModeVal || undefined;
    }
  }

  if (
    input.isSequenceFlow &&
    businessObject.conditionExpression?.body !== input.conditionExpression
  ) {
    if (input.conditionExpression.trim()) {
      const moddle = modeler.get('moddle');
      updates.conditionExpression = moddle.create('bpmn:FormalExpression', {
        body: input.conditionExpression.trim(),
      });
    } else {
      updates.conditionExpression = undefined;
    }
  }

  if (Object.keys(updates).length > 0) {
    modeling.updateProperties(element, updates);
  }
}
