import type { MenuDto, PermissionDto } from '#/api/role';

export interface PermissionGroup {
  key: string;
  label: string;
  level: number;
  permissions: PermissionDto[];
  selected: number;
  total: number;
}

const moduleOrder = [
  'asset',
  'category',
  'location',
  'file',
  'approval',
  'report',
  'project',
  'material',
  'material-flow',
  'user',
  'department',
  'role',
  'workflow',
  'setting',
  'audit',
  'backup',
  'admin',
];

const menuPermissionModules: Record<string, string[]> = {
  Admin: ['admin', 'audit', 'backup', 'department', 'role', 'setting', 'user', 'workflow'],
  AdminAudit: ['audit'],
  AdminBackups: ['backup'],
  AdminDepartments: ['department'],
  AdminRoles: ['role'],
  AdminSettings: ['setting'],
  AdminUsers: ['user'],
  AdminWorkflows: ['workflow'],
  Approval: ['approval'],
  ApprovalMine: ['approval'],
  ApprovalPending: ['approval'],
  Asset: ['asset', 'category', 'file', 'location'],
  AssetCategories: ['category'],
  AssetList: ['asset', 'file'],
  AssetLocations: ['location'],
  ConfirmReturn: ['approval'],
  Material: ['material', 'material-flow', 'project'],
  MaterialHome: ['project'],
  MaterialProjects: ['material', 'material-flow', 'project'],
  Report: ['report'],
  ReportBorrow: ['report'],
  ReportOverdue: ['report'],
  ReportSummary: ['report'],
};

interface BuildPermissionGroupsOptions {
  menus: MenuDto[];
  permissions: PermissionDto[];
  selectedPermissionIds: number[];
}

export function buildPermissionGroups({
  menus,
  permissions,
  selectedPermissionIds,
}: BuildPermissionGroupsOptions) {
  const selectedPermissionSet = new Set(selectedPermissionIds);
  const permissionsByModule = groupPermissionsByModule(permissions);

  function comparePermissions(a: PermissionDto, b: PermissionDto) {
    const ai = moduleOrder.indexOf(a.module || 'other');
    const bi = moduleOrder.indexOf(b.module || 'other');
    if (ai !== bi) {
      if (ai === -1) return 1;
      if (bi === -1) return -1;
      return ai - bi;
    }
    return a.code.localeCompare(b.code);
  }

  function collectMenuPermissionModules(menu: MenuDto) {
    const modules = new Set<string>();
    const configuredModules = menuPermissionModules[menu.name] ?? [];
    configuredModules.forEach((module) => modules.add(module));

    if (menu.permissionCode) {
      const module = menu.permissionCode.split(':')[0];
      if (module) modules.add(module);
    }

    return [...modules];
  }

  function collectMenuPermissions(menu: MenuDto) {
    const modulePermissions = collectMenuPermissionModules(menu)
      .flatMap((module) => permissionsByModule[module] ?? []);
    const codePermissions = menu.permissionCode
      ? permissions.filter((perm) => perm.code === menu.permissionCode)
      : [];

    return uniquePermissions([...modulePermissions, ...codePermissions])
      .sort(comparePermissions);
  }

  function toPermissionGroup(group: Omit<PermissionGroup, 'selected' | 'total'>): PermissionGroup {
    return {
      ...group,
      selected: group.permissions.filter((perm) => selectedPermissionSet.has(perm.id)).length,
      total: group.permissions.length,
    };
  }

  function buildMenuPermissionGroups(menuList: MenuDto[], level = 0): PermissionGroup[] {
    const groups: PermissionGroup[] = [];

    menuList
      .filter((menu) => menu.type !== 'button')
      .forEach((menu) => {
        const rawChildGroups = buildMenuPermissionGroups(menu.children ?? [], level + 1);
        const rawChildPermissionIds = new Set(
          rawChildGroups.flatMap((group) => group.permissions.map((perm) => perm.id)),
        );
        const rawOwnPermissions = collectMenuPermissions(menu)
          .filter((perm) => !rawChildPermissionIds.has(perm.id));

        if (rawOwnPermissions.length === 0 && hasSamePermissionSignature(rawChildGroups)) {
          groups.push(toPermissionGroup({
            key: `menu:${menu.id}`,
            label: menu.title || menu.name,
            level,
            permissions: rawChildGroups[0]!.permissions,
          }));
          return;
        }

        const childGroups = removeSubsetGroups(rawChildGroups);
        const childPermissionIds = new Set(
          childGroups.flatMap((group) => group.permissions.map((perm) => perm.id)),
        );
        const ownPermissions = collectMenuPermissions(menu)
          .filter((perm) => !childPermissionIds.has(perm.id));

        if (ownPermissions.length > 0) {
          groups.push(toPermissionGroup({
            key: `menu:${menu.id}`,
            label: menu.title || menu.name,
            level,
            permissions: ownPermissions,
          }));
          groups.push(...childGroups);
          return;
        }

        groups.push(...childGroups.map((group) => ({
          ...group,
          level: Math.max(0, group.level - 1),
        })));
      });

    return groups;
  }

  const groups = buildMenuPermissionGroups(menus);
  const coveredIds = new Set(groups.flatMap((group) => group.permissions.map((perm) => perm.id)));
  const ungroupedPermissions = permissions
    .filter((perm) => !coveredIds.has(perm.id))
    .sort(comparePermissions);

  if (ungroupedPermissions.length > 0) {
    groups.push(toPermissionGroup({
      key: '__ungrouped__',
      label: '未挂菜单权限',
      level: 0,
      permissions: ungroupedPermissions,
    }));
  }

  return groups;
}

function groupPermissionsByModule(permissions: PermissionDto[]) {
  const grouped: Record<string, PermissionDto[]> = {};
  permissions.forEach((perm) => {
    const module = perm.module || 'other';
    grouped[module] ??= [];
    grouped[module]!.push(perm);
  });
  return grouped;
}

function uniquePermissions(permissions: PermissionDto[]) {
  return permissions.filter(
    (perm, index, list) => list.findIndex((item) => item.id === perm.id) === index,
  );
}

function hasSamePermissionSignature(groups: PermissionGroup[]) {
  if (groups.length < 2) return false;
  const signatures = groups.map((group) => permissionSignature(group.permissions));
  return signatures.every((signature) => signature && signature === signatures[0]);
}

function permissionSignature(permissions: PermissionDto[]) {
  return permissions
    .map((perm) => perm.id)
    .sort((a, b) => a - b)
    .join(',');
}

function removeSubsetGroups(groups: PermissionGroup[]) {
  return groups.filter((group, index) => {
    const groupIds = new Set(group.permissions.map((perm) => perm.id));
    return !groups.some((candidate, candidateIndex) => {
      if (candidateIndex === index || candidate.permissions.length <= group.permissions.length) {
        return false;
      }
      const candidateIds = new Set(candidate.permissions.map((perm) => perm.id));
      return [...groupIds].every((id) => candidateIds.has(id));
    });
  });
}
