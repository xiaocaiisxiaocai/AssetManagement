import type { MenuDto, PermissionDto } from '#/api/role';

export function mergeMenuTreeSelection(
  checkedKeys: number[],
  halfCheckedKeys: number[],
) {
  return [...new Set([...checkedKeys, ...halfCheckedKeys])];
}

export function collectRequiredPermissionIds(
  menus: MenuDto[],
  permissions: PermissionDto[],
  selectedMenuIds: number[],
) {
  const selected = new Set(selectedMenuIds);
  const permissionIdByCode = new Map(
    permissions.map((item) => [item.code, item.id]),
  );

  return flattenMenus(menus).flatMap((menu) => {
    if (!selected.has(menu.id) || !menu.permissionCode) return [];
    const permissionId = permissionIdByCode.get(menu.permissionCode);
    return permissionId === undefined ? [] : [permissionId];
  });
}

export function filterPageMenuTree(menus: MenuDto[]): MenuDto[] {
  return menus
    .filter((menu) => menu.type !== 'button')
    .map((menu) => ({
      ...menu,
      children: filterPageMenuTree(menu.children ?? []),
    }));
}

function flattenMenus(items: MenuDto[]): MenuDto[] {
  return items.flatMap((item) => [item, ...flattenMenus(item.children ?? [])]);
}
