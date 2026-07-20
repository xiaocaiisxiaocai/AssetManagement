import type { AssetQuery } from '#/api/asset';

const approvalTypeLabels: Record<string, string> = {
  borrow: '借用',
  extension: '延期',
  return: '归还',
  transfer: '转让',
};

export function getApprovalAssetSelectCopy(type: string, keyword = '') {
  const typeLabel = approvalTypeLabels[type] || '';
  const eligibleLabel = typeLabel ? `可${typeLabel}` : '可操作';
  return {
    emptyText: keyword.trim()
      ? `未找到匹配的${eligibleLabel}资产`
      : `暂无${eligibleLabel}资产`,
    helpText: `只能选择当前${eligibleLabel}资产，支持按资产编号或名称搜索`,
    placeholder: `请选择或搜索${eligibleLabel}资产`,
  };
}

export function buildApprovalAssetQuery(
  type: string,
  currentUserId: number,
  keyword = '',
): AssetQuery {
  const query: AssetQuery = {
    keyword: keyword.trim() || undefined,
    page: 1,
    pageSize: 50,
  };
  if (type === 'borrow') query.status = 0;
  if (type === 'return' || type === 'extension') {
    query.custodianId = currentUserId;
    query.status = 1;
  }
  if (type === 'transfer') query.custodianId = currentUserId;
  return query;
}

export function mergeApprovalAssetOptions<T extends { id: number }>(
  current: T[],
  incoming: T[],
  selectedId?: number,
) {
  const selected = selectedId
    ? current.find((item) => item.id === selectedId)
    : undefined;
  const result = new Map(incoming.map((item) => [item.id, item]));
  if (selected && !result.has(selected.id)) result.set(selected.id, selected);
  return [...result.values()];
}
