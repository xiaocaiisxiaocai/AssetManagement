import type { ParsedCondition } from './bpmn-condition';

import { buildExpressionByField, parseCondition } from './bpmn-condition';
import {
  gatewaySupportsConditions,
  getSuggestedBranchName,
  getTargetNodeTitle,
  isDefaultGatewayBranch,
} from './gateway-branches';

export interface GatewayCondition extends ParsedCondition {
  flow: any;
  id: string;
  index: number;
  isDefault: boolean;
  isExplicitDefault: boolean;
  label: string;
  suggestedLabel: string;
  supportsCondition: boolean;
  targetName: string;
}

interface GatewayFlowUpdate {
  conditionExpression?: unknown;
  name?: string;
}

export function readGatewayConditions(
  element: any,
  gatewayType: string,
): GatewayCondition[] {
  const outgoing = element?.outgoing || [];
  const gatewayBusinessObject = element?.businessObject;
  return outgoing.map((flow: any, index: number) => {
    const businessObject = flow.businessObject;
    const expression = businessObject.conditionExpression?.body || '';
    const parsed = parseCondition(expression);
    const explicitDefault = gatewayBusinessObject?.default;
    const isExplicitDefault =
      explicitDefault === businessObject ||
      explicitDefault === businessObject.id ||
      explicitDefault?.id === businessObject.id;
    const source = {
      conditionExpression: expression,
      gatewayType,
      index,
      isExplicitDefault,
      name: businessObject.name,
      targetId: flow.target?.id,
      targetName: flow.target?.businessObject?.name,
      targetType: flow.target?.businessObject?.$type,
    };
    const suggestedLabel = getSuggestedBranchName(source);
    return {
      ...parsed,
      flow,
      id: flow.id,
      index,
      isDefault: isDefaultGatewayBranch(source),
      isExplicitDefault,
      label: businessObject.name?.trim() || suggestedLabel,
      suggestedLabel,
      supportsCondition: gatewaySupportsConditions(gatewayType),
      targetName: getTargetNodeTitle(source),
    };
  });
}

export function writeGatewayCondition(
  modeler: any,
  gatewayType: string,
  item: GatewayCondition,
) {
  const usedSuggestedLabel = item.label.trim() === item.suggestedLabel;
  item.expression = item.supportsCondition
    ? buildExpressionByField(item.field, item.operator, item.value)
    : '';
  item.isDefault = item.supportsCondition && !item.expression.trim();
  const nextSuggestedLabel = getSuggestedBranchName({
    conditionExpression: item.expression,
    gatewayType,
    index: item.index,
    isExplicitDefault: item.isExplicitDefault,
    targetId: item.flow.target?.id,
    targetName: item.flow.target?.businessObject?.name,
    targetType: item.flow.target?.businessObject?.$type,
  });
  if (usedSuggestedLabel) item.label = nextSuggestedLabel;
  item.suggestedLabel = nextSuggestedLabel;
  const normalizedLabel = item.label.trim() || item.suggestedLabel;
  item.label = normalizedLabel;

  const modeling = modeler.get('modeling');
  const moddle = modeler.get('moddle');
  const businessObject = item.flow.businessObject;
  const updates: GatewayFlowUpdate = {};

  if ((businessObject.name || '') !== normalizedLabel) {
    updates.name = normalizedLabel;
  }

  if (
    item.supportsCondition &&
    businessObject.conditionExpression?.body !== item.expression
  ) {
    updates.conditionExpression = item.expression
      ? moddle.create('bpmn:FormalExpression', { body: item.expression })
      : undefined;
  }

  if (Object.keys(updates).length > 0) {
    modeling.updateProperties(item.flow, updates);
  }
}

export function focusGatewayBranch(modeler: any, item: GatewayCondition) {
  modeler.get('selection').select(item.flow);
  modeler.get('canvas').scrollToElement(item.flow);
}
