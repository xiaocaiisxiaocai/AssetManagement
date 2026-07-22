export type AssetRowActionAccess = {
  canBorrow: boolean;
  canCreate: boolean;
  canDelete: boolean;
  canEdit: boolean;
  canExport: boolean;
  canImport: boolean;
  canPurge: boolean;
  canRestore: boolean;
  canTransfer: boolean;
  canView: boolean;
};

export function buildAssetRowActionAccess(
  hasAccess: (codes: string[]) => boolean,
): AssetRowActionAccess {
  return {
    canBorrow: hasAccess(['approval:create']),
    canCreate: hasAccess(['asset:create']),
    canDelete: hasAccess(['asset:delete']),
    canEdit: hasAccess(['asset:edit']),
    canExport: hasAccess(['asset:export']),
    canImport: hasAccess(['asset:import']),
    canPurge: hasAccess(['asset:purge']),
    canRestore: hasAccess(['asset:restore']),
    canTransfer: hasAccess(['approval:create']),
    canView: hasAccess(['asset:view']),
  };
}

export function canRunAvailableAssetAction(asset: {
  isDeleted: boolean;
  status: number;
}) {
  return !asset.isDeleted && asset.status === 0;
}

export function canBorrowAvailableAsset(
  asset: {
    custodianId?: null | number;
    isDeleted: boolean;
    status: number;
  },
  currentUserId: number,
) {
  return (
    canRunAvailableAssetAction(asset) && asset.custodianId !== currentUserId
  );
}

export function canTransferAvailableAsset(
  asset: {
    custodianId?: null | number;
    isDeleted: boolean;
    status: number;
  },
  currentUserId: number,
) {
  return (
    !asset.isDeleted &&
    (asset.status === 0 || asset.status === 1) &&
    asset.custodianId === currentUserId
  );
}
