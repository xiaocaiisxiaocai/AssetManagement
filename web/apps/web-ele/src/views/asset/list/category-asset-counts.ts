export interface CategoryTreeNode {
  children: CategoryTreeNode[];
  id: number;
}

export function countCategoryTreeAssets(
  node: CategoryTreeNode,
  directCounts: Record<string, number>,
): number {
  return (
    (directCounts[String(node.id)] ?? 0) +
    node.children.reduce(
      (sum, child) => sum + countCategoryTreeAssets(child, directCounts),
      0,
    )
  );
}
