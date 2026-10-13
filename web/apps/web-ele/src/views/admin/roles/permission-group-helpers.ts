import type { PermissionGroup } from './permission-groups';

import type { PermissionDto } from '#/api/role';

export function groupPermissionsByModule(permissions: PermissionDto[]) {
  const grouped: Record<string, PermissionDto[]> = {};
  permissions.forEach((perm) => {
    const module = perm.module || 'other';
    const modulePermissions = grouped[module] ?? (grouped[module] = []);
    modulePermissions.push(perm);
  });
  return grouped;
}

export function uniquePermissions(permissions: PermissionDto[]) {
  return permissions.filter(
    (perm, index, list) =>
      list.findIndex((item) => item.id === perm.id) === index,
  );
}

export function hasSamePermissionSignature(groups: PermissionGroup[]) {
  if (groups.length < 2) return false;
  const signatures = groups.map((group) =>
    permissionSignature(group.permissions),
  );
  return signatures.every(
    (signature) => signature && signature === signatures[0],
  );
}

function permissionSignature(permissions: PermissionDto[]) {
  return permissions
    .map((perm) => perm.id)
    .sort((a, b) => a - b)
    .join(',');
}

export function removeSubsetGroups(groups: PermissionGroup[]) {
  return groups.filter((group, index) => {
    const groupIds = new Set(group.permissions.map((perm) => perm.id));
    return !groups.some((candidate, candidateIndex) => {
      if (
        candidateIndex === index ||
        candidate.permissions.length <= group.permissions.length
      ) {
        return false;
      }
      const candidateIds = new Set(
        candidate.permissions.map((perm) => perm.id),
      );
      return [...groupIds].every((id) => candidateIds.has(id));
    });
  });
}
