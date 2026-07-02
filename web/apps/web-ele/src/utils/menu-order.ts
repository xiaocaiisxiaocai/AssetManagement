interface OrderedMenuLike {
  name?: PropertyKey;
  meta?: {
    order?: number;
  };
  sort?: number;
  children?: unknown[];
}

const builtInMenuOrder: Record<string, number> = {
  Home: 1,
  HomeWorkspace: 1,
  Asset: 10,
  AssetList: 11,
  Material: 15,
  MaterialHome: 16,
  MaterialProjects: 17,
  AssetCategories: 18,
  AssetLocations: 19,
  Approval: 20,
  ApprovalPending: 21,
  ApprovalMine: 22,
  ConfirmReturn: 23,
  Report: 30,
  ReportSummary: 31,
  ReportBorrow: 32,
  ReportOverdue: 33,
  Admin: 40,
  AdminUsers: 41,
  AdminRoles: 42,
  AdminDepartments: 43,
  AdminWorkflows: 44,
  AdminSettings: 45,
  AdminAudit: 46,
  AdminBackups: 47,
};

function orderOf(item: OrderedMenuLike) {
  const name = typeof item.name === 'string' ? item.name : '';
  const builtInOrder = builtInMenuOrder[name];
  if (builtInOrder !== undefined) {
    return builtInOrder;
  }
  return item.meta?.order ?? item.sort ?? 9999;
}

function cloneWithSortedChildren<T extends OrderedMenuLike>(item: T): T {
  const children = Array.isArray(item.children) && item.children.length > 0
    ? sortBuiltInMenus(item.children as OrderedMenuLike[])
    : item.children;
  return {
    ...item,
    ...(children ? { children } : {}),
  };
}

export function sortBuiltInMenus<T extends OrderedMenuLike>(items: T[]): T[] {
  return [...items]
    .map((item) => cloneWithSortedChildren(item))
    .sort((a, b) => {
      const orderDiff = orderOf(a) - orderOf(b);
      if (orderDiff !== 0) return orderDiff;
      const aName = typeof a.name === 'string' ? a.name : '';
      const bName = typeof b.name === 'string' ? b.name : '';
      return aName.localeCompare(bName);
    });
}
