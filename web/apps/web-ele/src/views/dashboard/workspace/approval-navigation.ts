export interface ApprovalNavigationAccess {
  canHandleApprovals: boolean;
  canHandleMaterialFlows: boolean;
  canViewApprovals: boolean;
  canViewMaterialFlows: boolean;
}

export function pendingApprovalPath(access: ApprovalNavigationAccess) {
  if (access.canHandleApprovals) return '/approval/pending';
  if (access.canHandleMaterialFlows)
    return '/material/approvals?source=material';
  return null;
}

export function myApplicationPath(access: ApprovalNavigationAccess) {
  if (access.canViewApprovals) return '/approval/mine';
  if (access.canViewMaterialFlows)
    return '/material/applications?source=material';
  return null;
}
