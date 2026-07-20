import type { MaterialItem } from '#/api/material';

export interface MaterialTransferContext {
  currentUserId: number;
  isSupervisor: boolean;
  projectOwnerId?: null | number;
}

export function canTransferMaterial(
  material: MaterialItem,
  context: MaterialTransferContext,
) {
  if (material.isDeleted || material.status !== 0 || material.hasPendingFlow)
    return false;
  return (
    context.isSupervisor ||
    material.custodianId === context.currentUserId ||
    context.projectOwnerId === context.currentUserId
  );
}
