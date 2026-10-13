import { ElMessage } from 'element-plus';

import {
  getBranchLabelDelta,
  getGatewayValidationError,
  getSuggestedBranchName,
  isGatewayDiagramNode,
} from './gateway-branches';

export function createGatewayBranchTools(modeler: { value?: any }) {
  let branchNormalizationQueued = false;
  let isNormalizingBranches = false;

  function normalizeGatewayBranchLabels() {
    if (!modeler.value || isNormalizingBranches) return 0;

    const elementRegistry = modeler.value.get('elementRegistry');
    const modeling = modeler.value.get('modeling');
    const gateways = elementRegistry
      .getAll()
      .filter((element: any) => isGatewayDiagramNode(element));
    let normalizedCount = 0;

    isNormalizingBranches = true;
    try {
      for (const gateway of gateways) {
        const gatewayType = gateway.businessObject.$type;
        const explicitDefault = gateway.businessObject.default;
        for (const [index, flow] of (gateway.outgoing || []).entries()) {
          const businessObject = flow.businessObject;
          if (businessObject?.name?.trim()) continue;

          const isExplicitDefault =
            explicitDefault === businessObject ||
            explicitDefault === businessObject?.id ||
            explicitDefault?.id === businessObject?.id;
          modeling.updateProperties(flow, {
            name: getSuggestedBranchName({
              conditionExpression: businessObject?.conditionExpression?.body,
              gatewayType,
              index,
              isExplicitDefault,
              targetId: flow.target?.id,
              targetName: flow.target?.businessObject?.name,
              targetType: flow.target?.businessObject?.$type,
            }),
          });
          if (flow.label && flow.waypoints?.length > 1) {
            const delta = getBranchLabelDelta(flow.waypoints, flow.label, {
              x: gateway.x + gateway.width / 2,
              y: gateway.y + gateway.height / 2,
            });
            if (Math.abs(delta.x) > 1 || Math.abs(delta.y) > 1) {
              modeling.moveShape(flow.label, delta);
            }
          }
          normalizedCount += 1;
        }
      }
    } finally {
      isNormalizingBranches = false;
    }

    return normalizedCount;
  }

  function scheduleGatewayBranchNormalization() {
    if (branchNormalizationQueued || isNormalizingBranches) return;

    branchNormalizationQueued = true;
    queueMicrotask(() => {
      branchNormalizationQueued = false;
      normalizeGatewayBranchLabels();
    });
  }

  function validateGatewayBranches() {
    const elementRegistry = modeler.value.get('elementRegistry');
    let validationError = '';
    const invalidGateway = elementRegistry.getAll().find((element: any) => {
      if (!isGatewayDiagramNode(element)) return false;

      const gatewayType = element.businessObject?.$type;
      if (
        gatewayType !== 'bpmn:ExclusiveGateway' &&
        gatewayType !== 'bpmn:ParallelGateway'
      ) {
        return false;
      }

      const error = getGatewayValidationError({
        expressions: (element.outgoing || []).map(
          (flow: any) => flow.businessObject?.conditionExpression?.body || '',
        ),
        gatewayName: element.businessObject?.name || element.id,
        gatewayType,
      });
      if (!error) return false;
      validationError = error;
      return true;
    });

    if (!invalidGateway) return true;
    modeler.value.get('selection').select(invalidGateway);
    modeler.value.get('canvas').scrollToElement(invalidGateway);
    ElMessage.error(validationError);
    return false;
  }

  return {
    normalizeGatewayBranchLabels,
    scheduleGatewayBranchNormalization,
    validateGatewayBranches,
  };
}
