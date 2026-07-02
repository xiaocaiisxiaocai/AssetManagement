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

export function buildAssetRowActionAccess(hasAccess: (codes: string[]) => boolean): AssetRowActionAccess {
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
