import { requestClient } from '#/api/request';

import { unwrap } from './unwrap';

export interface AssetOverviewCategory {
  available: number;
  borrowed: number;
  maintenance: number;
  scrapped: number;
  categoryCode: string;
  categoryId: number;
  label: string;
  parentCode: string;
  remark: null | string;
  total: number;
}

export interface AssetOverview {
  available: number;
  borrowed: number;
  maintenance: number;
  scrapped: number;
  categories: AssetOverviewCategory[];
  total: number;
  uncategorizedTotal: number;
}

export const getAssetOverviewApi = () =>
  unwrap(
    requestClient.get<{ code: number; data: AssetOverview; message: string }>(
      '/assets/overview',
    ),
  );
