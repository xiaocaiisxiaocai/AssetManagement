export type HasAccess = (codes: string[]) => boolean;

export function buildCategoryActionAccess(hasAccess: HasAccess) {
  return {
    canCreate: hasAccess(['category:create']),
    canDelete: hasAccess(['category:delete']),
    canEdit: hasAccess(['category:edit']),
    canPurge: hasAccess(['category:purge']),
    canRestore: hasAccess(['category:restore']),
  };
}

export function buildDepartmentActionAccess(hasAccess: HasAccess) {
  return {
    canCreate: hasAccess(['department:create']),
    canDelete: hasAccess(['department:delete']),
    canEdit: hasAccess(['department:edit']),
  };
}

export function buildRoleActionAccess(hasAccess: HasAccess) {
  return {
    canAssignMenu: hasAccess(['role:assign-menu']),
    canAssignPermission: hasAccess(['role:assign-permission']),
    canCreate: hasAccess(['role:create']),
    canDelete: hasAccess(['role:delete']),
    canEdit: hasAccess(['role:edit']),
  };
}

export function buildUserActionAccess(hasAccess: HasAccess) {
  return {
    canCreate: hasAccess(['user:create']),
    canDelete: hasAccess(['user:delete']),
    canEdit: hasAccess(['user:edit']),
    canResetPassword: hasAccess(['user:reset-password']),
    canToggleStatus: hasAccess(['user:toggle-status']),
  };
}

export function buildProjectActionAccess(hasAccess: HasAccess) {
  return {
    canCreate: hasAccess(['project:create']),
    canDelete: hasAccess(['project:delete']),
    canEdit: hasAccess(['project:edit']),
    canFollowup: hasAccess(['project:followup']),
    canOption: hasAccess(['project:option']),
    canPurge: hasAccess(['project:purge']),
    canRestore: hasAccess(['project:restore']),
  };
}

export function buildMaterialActionAccess(hasAccess: HasAccess) {
  return {
    canApprove: hasAccess(['material-flow:approve']),
    canCreate: hasAccess(['material:create']),
    canDelete: hasAccess(['material:delete']),
    canEdit: hasAccess(['material:edit']),
    canPurge: hasAccess(['material:purge']),
    canRestore: hasAccess(['material:restore']),
    canReturn: hasAccess(['material:return']),
    canTransfer: hasAccess(['material-flow:transfer']),
  };
}

export function buildLocationActionAccess(hasAccess: HasAccess) {
  return {
    canCreate: hasAccess(['location:create']),
    canDelete: hasAccess(['location:delete']),
    canEdit: hasAccess(['location:edit']),
  };
}

export function buildWorkflowActionAccess(hasAccess: HasAccess) {
  return {
    canCreate: hasAccess(['workflow:create']),
    canDelete: hasAccess(['workflow:delete']),
    canDesign: hasAccess(['workflow:design']) || hasAccess(['workflow:edit']),
    canEdit: hasAccess(['workflow:edit']),
  };
}

export function buildReportActionAccess(hasAccess: HasAccess) {
  return {
    canExport: hasAccess(['report:export']),
    canRemind: hasAccess(['report:remind']),
  };
}
