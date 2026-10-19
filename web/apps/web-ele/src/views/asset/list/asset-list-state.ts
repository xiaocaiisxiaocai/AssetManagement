import type { AssetDetail, AssetItem, AssetStatus } from '#/api/asset';
import type {
  CategoryNode,
  DepartmentNode,
  DepartmentOptionNode,
} from '#/api/base-data';
import type { UserOptionDto } from '#/api/user';

import { reactive, ref } from 'vue';

import { createLatestRequestGuard } from '#/utils/latest-request';
import { reactiveObjectModel } from '#/utils/reactive-object-model';
import { createPageSizeOptions } from '#/utils/runtime-settings';

export type AssetListQueryState = {
  assetNo: string;
  categoryId: number | undefined;
  custodianIds: number[];
  deleteStatus: 'active' | 'all' | 'deleted';
  departmentIds: number[];
  name: string;
  page: number;
  pageSize: number;
  statuses: AssetStatus[];
};

export function createAssetListState() {
  const loading = ref(false);
  const deletingAssetIds = ref<number[]>([]);
  const dialogVisible = ref(false);
  const importDialogVisible = ref(false);
  const borrowDialogVisible = ref(false);
  const transferDialogVisible = ref(false);
  const editingAsset = ref<AssetItem | null>(null);
  const formDefaultCategoryId = ref(0);
  const selectedCategoryId = ref<null | number>(null);
  const flatMode = ref(true);
  const assets = ref<AssetItem[]>([]);
  const categoryAssetCounts = ref<Record<string, number>>({});
  const total = ref(0);
  const categoryPath = ref<number[]>([]);
  const categoryPage = ref(1);
  const categoryPageSize = ref(20);
  const hierarchyKeyword = ref('');
  const categories = ref<CategoryNode[]>([]);
  const departments = ref<(DepartmentNode | DepartmentOptionNode)[]>([]);
  const users = ref<UserOptionDto[]>([]);
  const currentAssetForAction = ref<AssetItem | null>(null);
  const detailVisible = ref(false);
  const detailLoading = ref(false);
  const detailRequestGuard = createLatestRequestGuard();
  const listRequestGuard = createLatestRequestGuard();
  const userOptionsLoading = ref(false);
  const userOptionsRequestGuard = createLatestRequestGuard();
  const detail = ref<AssetDetail | null>(null);
  const exportingAllAssets = ref(false);
  const pageSizeOptions = ref(createPageSizeOptions(20));
  const query = reactive<AssetListQueryState>({
    assetNo: '',
    categoryId: undefined,
    custodianIds: [],
    departmentIds: [],
    deleteStatus: 'all',
    name: '',
    page: 1,
    pageSize: 20,
    statuses: [],
  });
  const queryModel = reactiveObjectModel(query);

  return {
    assets,
    borrowDialogVisible,
    categories,
    categoryAssetCounts,
    categoryPage,
    categoryPageSize,
    categoryPath,
    currentAssetForAction,
    deletingAssetIds,
    departments,
    detail,
    detailLoading,
    detailRequestGuard,
    detailVisible,
    dialogVisible,
    editingAsset,
    exportingAllAssets,
    flatMode,
    formDefaultCategoryId,
    hierarchyKeyword,
    importDialogVisible,
    listRequestGuard,
    loading,
    pageSizeOptions,
    query,
    queryModel,
    selectedCategoryId,
    total,
    transferDialogVisible,
    userOptionsLoading,
    userOptionsRequestGuard,
    users,
  };
}

export type AssetListState = ReturnType<typeof createAssetListState>;
export type LatestRequestGuard = AssetListState['listRequestGuard'];
