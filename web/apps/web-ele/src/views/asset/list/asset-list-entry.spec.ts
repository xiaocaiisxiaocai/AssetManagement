import type { RouteLocationNormalizedLoaded } from 'vue-router';

import type { CategoryNode } from '#/api/base-data';

import { beforeEach, describe, expect, it, vi } from 'vitest';

import { bindAssetHierarchy } from './asset-hierarchy';
import { bindAssetListLoading } from './asset-list-browse';
import { bindAssetHierarchyDrill } from './asset-list-drill';
import { createAssetListState } from './asset-list-state';

const api = vi.hoisted(() => ({ getAssetListApi: vi.fn() }));

vi.mock('#/api/asset', () => ({
  exportAssetsApi: vi.fn(),
  getAssetCategoryCountsApi: vi.fn(),
  getAssetListApi: api.getAssetListApi,
}));

vi.mock('#/api/base-data', () => ({ getRuntimeSettingsApi: vi.fn() }));

function category(
  id: number,
  code: string,
  children: CategoryNode[] = [],
): CategoryNode {
  return {
    children,
    code,
    codeSeg: code.split('-').at(-1) ?? code,
    id,
    isDeleted: false,
  };
}

function setup() {
  const state = createAssetListState();
  state.categories.value = [
    category(1, 'RD', [category(2, 'RD-CAM', [category(3, 'RD-CAM-ONT')])]),
    category(4, 'IT', [category(5, 'IT-PC', [category(6, 'IT-PC-NB')])]),
  ];
  const hierarchy = bindAssetHierarchy(state);
  const list = bindAssetListLoading({ ...state, ...hierarchy });
  const route = { query: {} } as RouteLocationNormalizedLoaded;
  const drill = bindAssetHierarchyDrill(
    { ...state, ...hierarchy, route },
    list.loadData,
  );
  return { ...state, ...hierarchy, ...list, ...drill, route };
}

describe('资产列表默认入口与分类跳转', () => {
  beforeEach(() => {
    api.getAssetListApi.mockReset();
    api.getAssetListApi.mockResolvedValue({ items: [], total: 0 });
  });

  it('默认直接显示明细表，请求不限制分类并保留分页和删除状态口径', async () => {
    const page = setup();
    expect(page.showAssetTable.value).toBe(true);
    expect(page.currentLevelTitle.value).toBe('全部资产清单');
    expect(await page.applyCategoryCodeFromRoute()).toBe(false);
    await page.loadData();
    expect(api.getAssetListApi).toHaveBeenLastCalledWith(
      expect.objectContaining({
        categoryId: undefined,
        deleteStatus: 'all',
        page: 1,
        pageSize: 20,
      }),
    );
  });

  it('报表跳转到末级分类仍显示该分类明细，清除参数后恢复全部资产', async () => {
    const page = setup();
    page.route.query.categoryCode = 'RD-CAM-ONT';
    expect(await page.applyCategoryCodeFromRoute()).toBe(true);
    expect(page.showAssetTable.value).toBe(true);
    expect(api.getAssetListApi).toHaveBeenLastCalledWith(
      expect.objectContaining({ categoryId: 3 }),
    );
    page.route.query = {};
    expect(await page.applyCategoryCodeFromRoute()).toBe(false);
    expect(page.showAssetTable.value).toBe(true);
    expect(page.buildQuery().categoryId).toBeUndefined();
    expect(page.selectedCategoryId.value).toBeNull();
    expect(page.categoryPath.value).toEqual([]);
  });

  it('末级分类中重置筛选保留范围，无效分类参数恢复全部明细', async () => {
    const page = setup();
    page.route.query.categoryCode = 'RD-CAM-ONT';
    await page.applyCategoryCodeFromRoute();
    expect(page.showAssetTable.value).toBe(true);
    expect(page.currentLevelTitle.value).toBe('资产清单 - RD-CAM-ONT');
    page.query.name = '摄像机';
    page.query.page = 3;
    page.resetQuery();
    expect(page.buildQuery()).toEqual(
      expect.objectContaining({ categoryId: 3, name: undefined, page: 1 }),
    );
    page.route.query.categoryCode = '不存在的分类';
    await page.applyCategoryCodeFromRoute();
    expect(page.currentLevelTitle.value).toBe('全部资产清单');
    expect(page.query.categoryId).toBeUndefined();
  });

  it('可选分类浏览仍能逐级进入末级，再切回全部明细', () => {
    const page = setup();
    page.exitFlatMode();
    expect(page.showAssetTable.value).toBe(false);
    const root = page.categories.value[0];
    const second = root?.children[0];
    const leaf = second?.children[0];
    if (!root || !second || !leaf) throw new Error('测试分类树缺少层级');
    page.drillIntoCategory(root);
    page.drillIntoCategory(second);
    page.drillIntoCategory(leaf);
    expect(page.showAssetTable.value).toBe(true);
    expect(page.query.categoryId).toBe(3);
    page.enterFlatMode();
    expect(page.currentLevelTitle.value).toBe('全部资产清单');
    expect(page.query.categoryId).toBeUndefined();
  });

  it('没有分类目录权限或目录为空时也能直接查看全部资产', async () => {
    const page = setup();
    page.categories.value = [];
    expect(await page.applyCategoryCodeFromRoute()).toBe(false);
    expect(page.showAssetTable.value).toBe(true);
    await page.loadData();
    expect(api.getAssetListApi).toHaveBeenLastCalledWith(
      expect.objectContaining({ categoryId: undefined }),
    );
  });
});
