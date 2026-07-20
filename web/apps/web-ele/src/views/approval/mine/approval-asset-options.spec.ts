import { describe, expect, it } from 'vitest';

import {
  buildApprovalAssetQuery,
  getApprovalAssetSelectCopy,
  mergeApprovalAssetOptions,
} from './approval-asset-options';

describe('审批资产远程选项', () => {
  it('借用只请求一页在库资产并携带远程关键字', () => {
    expect(buildApprovalAssetQuery('borrow', 7, ' ZC-001 ')).toEqual({
      keyword: 'ZC-001',
      page: 1,
      pageSize: 50,
      status: 0,
    });
  });

  it('远程搜索后保留不在新结果中的已选资产', () => {
    expect(
      mergeApprovalAssetOptions(
        [{ id: 7, name: '已选资产' }],
        [{ id: 8, name: '搜索结果' }],
        7,
      ),
    ).toEqual([
      { id: 8, name: '搜索结果' },
      { id: 7, name: '已选资产' },
    ]);
  });

  it('归还只查询当前保管人的借出资产', () => {
    expect(buildApprovalAssetQuery('return', 7)).toMatchObject({
      custodianId: 7,
      status: 1,
    });
  });

  it('延期只查询当前保管人的借出资产', () => {
    expect(buildApprovalAssetQuery('extension', 7)).toMatchObject({
      custodianId: 7,
      status: 1,
    });
  });

  it.each([
    ['borrow', '可借用'],
    ['extension', '可延期'],
    ['transfer', '可转让'],
    ['return', '可归还'],
  ])('%s 类型显示对应的可选范围', (type, label) => {
    const copy = getApprovalAssetSelectCopy(type);

    expect(copy.placeholder).toContain(label);
    expect(copy.helpText).toContain(label);
    expect(copy.emptyText).toBe(`暂无${label}资产`);
  });

  it('输入搜索词但没有结果时给出匹配失败提示', () => {
    expect(getApprovalAssetSelectCopy('borrow', '电脑').emptyText).toBe(
      '未找到匹配的可借用资产',
    );
  });
});
