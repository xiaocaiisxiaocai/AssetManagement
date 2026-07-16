export interface GatewayBranchSource {
  conditionExpression?: string;
  gatewayType: string;
  index: number;
  isExplicitDefault?: boolean;
  name?: string;
  targetId?: string;
  targetName?: string;
  targetType?: string;
}

export interface GatewayValidationSource {
  expressions: string[];
  gatewayName: string;
  gatewayType: string;
}

interface DiagramElementLike {
  businessObject?: {
    $type?: string;
  };
  id?: string;
  labelTarget?: DiagramElementLike | null;
  type?: string;
}

interface Point {
  x: number;
  y: number;
}

interface Rect extends Point {
  height: number;
  width: number;
}

const targetTypeNames: Record<string, string> = {
  'bpmn:EndEvent': '流程结束',
  'bpmn:ExclusiveGateway': '下一个条件分支',
  'bpmn:InclusiveGateway': '下一个包容分支',
  'bpmn:ParallelGateway': '下一个并行节点',
  'bpmn:ServiceTask': '自动任务',
  'bpmn:StartEvent': '流程开始',
  'bpmn:UserTask': '审批节点',
};

const gatewayTypes = new Set([
  'bpmn:ExclusiveGateway',
  'bpmn:InclusiveGateway',
  'bpmn:ParallelGateway',
]);

export function resolveDiagramElement<T extends DiagramElementLike>(
  element: T | null | undefined,
) {
  return element?.labelTarget || element;
}

export function isGatewayDiagramNode(element: DiagramElementLike) {
  return (
    element.type !== 'label' &&
    !element.labelTarget &&
    gatewayTypes.has(element.businessObject?.$type || '')
  );
}

export function gatewaySupportsConditions(gatewayType: string) {
  return (
    gatewayType === 'bpmn:ExclusiveGateway' ||
    gatewayType === 'bpmn:InclusiveGateway'
  );
}

export function getTargetNodeTitle(source: GatewayBranchSource) {
  const name = source.targetName?.trim();
  if (name) return name;

  if (!source.targetId) return '未连接节点';
  return targetTypeNames[source.targetType || ''] || source.targetId;
}

export function isDefaultGatewayBranch(source: GatewayBranchSource) {
  if (source.gatewayType === 'bpmn:ParallelGateway') return false;
  return !source.conditionExpression?.trim();
}

export function getConditionSummary(expression?: string) {
  const match = expression
    ?.trim()
    .match(
      /^\$\{(applicantDept|applicantRole|isProjectOwner)\}\s*==\s*["'](.+)["']$/,
    );
  if (!match) return '';

  const [, field, value] = match;
  if (field === 'isProjectOwner') {
    return value === 'true' ? '是项目负责人' : '非项目负责人';
  }
  if (field === 'applicantRole') return `申请角色：${value}`;
  return `申请部门：${value}`;
}

export function getSuggestedBranchName(source: GatewayBranchSource) {
  const storedName = source.name?.trim();
  if (storedName) return storedName;

  const targetName = getTargetNodeTitle(source);
  if (source.gatewayType === 'bpmn:ParallelGateway') {
    return `并行至「${targetName}」`;
  }
  if (isDefaultGatewayBranch(source)) return '其他情况（默认）';

  return (
    getConditionSummary(source.conditionExpression) ||
    `条件分支 ${source.index + 1}`
  );
}

export function getBranchLabelDelta(
  waypoints: Point[],
  label: Rect,
  sourceCenter: Point,
) {
  const segments = waypoints.slice(1).map((end, index) => {
    const start = waypoints[index]!;
    return {
      end,
      horizontal: Math.abs(end.y - start.y) < 1,
      length: Math.hypot(end.x - start.x, end.y - start.y),
      start,
    };
  });
  const candidates = segments.filter((segment) => segment.horizontal);
  const segment = (candidates.length > 0 ? candidates : segments).sort(
    (left, right) => right.length - left.length,
  )[0];
  if (!segment) return { x: 0, y: 0 };

  const center = {
    x: (segment.start.x + segment.end.x) / 2,
    y: (segment.start.y + segment.end.y) / 2,
  };
  const desired = segment.horizontal
    ? {
        x: center.x - label.width / 2,
        y:
          center.y <= sourceCenter.y
            ? center.y - label.height - 8
            : center.y + 8,
      }
    : {
        x: center.x + 8,
        y: center.y - label.height / 2,
      };

  return {
    x: desired.x - label.x,
    y: desired.y - label.y,
  };
}

export function getGatewayValidationError(source: GatewayValidationSource) {
  if (source.gatewayType === 'bpmn:ExclusiveGateway') {
    const defaultCount = source.expressions.filter(
      (expression) => !expression.trim(),
    ).length;
    if (defaultCount !== 1) {
      return `条件分支「${source.gatewayName}」当前有 ${defaultCount} 条默认分支，应且只能保留 1 条无条件分支`;
    }
  }

  if (
    source.gatewayType === 'bpmn:ParallelGateway' &&
    source.expressions.some((expression) => expression.trim())
  ) {
    return `并行网关「${source.gatewayName}」会同时执行所有分支，不能设置条件`;
  }

  return '';
}
