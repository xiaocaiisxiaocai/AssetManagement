export interface ActionableWorkflowFlow {
  actionableNodeIds: string[];
  bpmnTokens?: Record<string, { nodeName?: string }>;
}

export interface ActionableWorkflowNode {
  id: string;
  name: string;
}

export function getActionableWorkflowNodes(
  flow: ActionableWorkflowFlow,
): ActionableWorkflowNode[] {
  return flow.actionableNodeIds.map((id) => ({
    id,
    name: flow.bpmnTokens?.[id]?.nodeName || id,
  }));
}

export function getDirectActionableWorkflowNode(
  flow: ActionableWorkflowFlow,
): ActionableWorkflowNode | undefined {
  const nodes = getActionableWorkflowNodes(flow);
  return nodes.length === 1 ? nodes[0] : undefined;
}

export function findActionableWorkflowNode(
  flow: ActionableWorkflowFlow,
  nodeId: string,
): ActionableWorkflowNode | undefined {
  return getActionableWorkflowNodes(flow).find((node) => node.id === nodeId);
}

export function formatWorkflowNode(node: ActionableWorkflowNode): string {
  return `${node.name}（${node.id}）`;
}

export function formatActionableWorkflowNode(
  flow: ActionableWorkflowFlow,
): string {
  const nodes = getActionableWorkflowNodes(flow);
  const node = nodes[0];
  if (!node) return '无可操作节点';

  if (nodes.length === 1) return formatWorkflowNode(node);
  return `${node.name}等 ${nodes.length} 个可操作节点`;
}
