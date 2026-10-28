export function resolveNotificationRoute(type: string, flowId?: number) {
  const query = flowId
    ? {
        flowId: String(flowId),
        source: type.startsWith('material_') ? 'material' : 'asset',
      }
    : undefined;

  if (
    type === 'approval_pending' ||
    type === 'approval_reminder' ||
    type === 'material_approval_pending' ||
    type === 'material_approval_reminder'
  ) {
    return { path: '/approval/pending', query };
  }

  if (
    type === 'approval_approved' ||
    type === 'approval_rejected' ||
    type === 'approval_released' ||
    type === 'return_confirmed'
  ) {
    return { path: '/approval/mine', query };
  }

  if (type === 'transfer_received') {
    return { path: '/asset/list' };
  }

  if (type === 'material_receipt_pending') {
    return {
      path: '/material/projects',
      query: {
        ...(flowId ? { flowId: String(flowId) } : {}),
        source: 'material',
        intent: 'receive',
      },
    };
  }

  if (
    type === 'material_received' ||
    type === 'material_approved' ||
    type === 'material_rejected'
  ) {
    return { path: '/approval/mine', query };
  }

  if (type.startsWith('material_')) {
    return { path: '/approval/mine', query };
  }

  if (type === 'overdue' || type.startsWith('due_soon')) {
    return { path: '/approval/mine', query };
  }

  return { path: '/approval/pending' };
}
