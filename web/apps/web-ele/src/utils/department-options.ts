import type { DepartmentNode, DepartmentOptionNode } from '#/api/base-data';

export interface DepartmentOption {
  id: number;
  isActive: boolean;
  label: string;
  managerId?: number;
  managerName?: string;
}

export function flattenActiveDepartments(
  nodes: (DepartmentNode | DepartmentOptionNode)[],
  level = 0,
): DepartmentOption[] {
  return nodes.flatMap((node) => {
    if ('isActive' in node && !node.isActive) {
      return [];
    }

    return [
      {
        id: node.id,
        isActive: true,
        label: `${'　'.repeat(level)}${node.name}`,
        ...(node.managerId === null || node.managerId === undefined
          ? {}
          : {
              managerId: node.managerId,
              managerName: node.managerName ?? undefined,
            }),
      },
      ...flattenActiveDepartments(node.children, level + 1),
    ];
  });
}
