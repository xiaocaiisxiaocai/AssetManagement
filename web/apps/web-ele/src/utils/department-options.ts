import type { DepartmentNode } from '#/api/base-data';

export interface DepartmentOption {
  id: number;
  isActive: boolean;
  label: string;
}

export function flattenActiveDepartments(
  nodes: DepartmentNode[],
  level = 0,
): DepartmentOption[] {
  return nodes.flatMap((node) => {
    if (!node.isActive) {
      return [];
    }

    return [
      {
        id: node.id,
        isActive: true,
        label: `${'　'.repeat(level)}${node.name}`,
      },
      ...flattenActiveDepartments(node.children, level + 1),
    ];
  });
}
