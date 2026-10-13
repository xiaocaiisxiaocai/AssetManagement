import type { CategoryNode } from '#/api/base-data';

import type { Ref } from 'vue';
import { computed } from 'vue';

export type AssetHierarchyContext = {
  nodes: CategoryNode[];
  parent: CategoryNode | null;
  trail: CategoryNode[];
};

export function bindAssetHierarchy(input: {
  categories: Ref<CategoryNode[]>;
  categoryPage: Ref<number>;
  categoryPageSize: Ref<number>;
  categoryPath: Ref<number[]>;
  flatMode: Ref<boolean>;
  hierarchyKeyword: Ref<string>;
}) {
  const {
    categories,
    categoryPage,
    categoryPageSize,
    categoryPath,
    flatMode,
    hierarchyKeyword,
  } = input;
  const MAX_CATEGORY_LEVEL = 3;

  function getHierarchyContext(): AssetHierarchyContext {
    let nodes = categories.value;
    let parent: CategoryNode | null = null;
    const trail: CategoryNode[] = [];
    for (const id of categoryPath.value) {
      const node = nodes.find((item) => item.id === id);
      if (!node) break;
      parent = node;
      trail.push(node);
      nodes = node.children;
    }
    return { nodes, parent, trail };
  }

  function findCategoryPathByCode(
    nodes: CategoryNode[],
    code: string,
    trail: number[] = [],
  ): number[] {
    for (const node of nodes) {
      const nextTrail = [...trail, node.id];
      if (node.code === code) {
        return nextTrail;
      }
      const childTrail = findCategoryPathByCode(node.children, code, nextTrail);
      if (childTrail.length > 0) {
        return childTrail;
      }
    }
    return [];
  }

  const hierarchyContext = computed(() => getHierarchyContext());
  const hierarchyNodes = computed(() => hierarchyContext.value.nodes);
  const hierarchyParent = computed(() => hierarchyContext.value.parent);
  const hierarchyTrail = computed(() => hierarchyContext.value.trail);
  const currentCategoryLevel = computed(() => categoryPath.value.length);
  const isAssetStage = computed(
    () =>
      currentCategoryLevel.value === MAX_CATEGORY_LEVEL &&
      !!hierarchyParent.value,
  );
  const isCategoryStage = computed(
    () => currentCategoryLevel.value < MAX_CATEGORY_LEVEL,
  );
  const showAssetTable = computed(() => isAssetStage.value || flatMode.value);
  const currentLevelTitle = computed(() => {
    if (flatMode.value) return '全部资产清单';
    if (currentCategoryLevel.value === 0) return '一级分类';
    if (currentCategoryLevel.value === 1)
      return `二级分类 - ${hierarchyParent.value?.code ?? ''}`;
    if (currentCategoryLevel.value === 2)
      return `三级分类 - ${hierarchyParent.value?.code ?? ''}`;
    return `资产清单 - ${hierarchyParent.value?.code ?? ''}`;
  });
  const nextLevelName = computed(() => {
    const nextLevel = currentCategoryLevel.value + 1;
    if (nextLevel === 1) return '一级分类';
    if (nextLevel === 2) return '二级分类';
    return '三级分类';
  });
  const filteredHierarchyNodes = computed(() => {
    const keyword = hierarchyKeyword.value.trim().toLowerCase();
    if (!keyword) return hierarchyNodes.value;
    return hierarchyNodes.value.filter((node) =>
      `${node.code} ${node.remark ?? ''}`.toLowerCase().includes(keyword),
    );
  });
  const pagedHierarchyNodes = computed(() => {
    const start = (categoryPage.value - 1) * categoryPageSize.value;
    return filteredHierarchyNodes.value.slice(
      start,
      start + categoryPageSize.value,
    );
  });

  return {
    MAX_CATEGORY_LEVEL,
    currentCategoryLevel,
    currentLevelTitle,
    filteredHierarchyNodes,
    findCategoryPathByCode,
    getHierarchyContext,
    hierarchyContext,
    hierarchyNodes,
    hierarchyParent,
    hierarchyTrail,
    isAssetStage,
    isCategoryStage,
    nextLevelName,
    pagedHierarchyNodes,
    showAssetTable,
  };
}

export type AssetHierarchyModel = ReturnType<typeof bindAssetHierarchy>;
