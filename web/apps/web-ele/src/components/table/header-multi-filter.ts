export type HeaderFilterValue = number | string;

export interface HeaderFilterOption<
  T extends HeaderFilterValue = HeaderFilterValue,
> {
  label: string;
  value: T;
}

export interface HeaderFilterGroup<
  T extends HeaderFilterValue = HeaderFilterValue,
> {
  label?: string;
  options: HeaderFilterOption<T>[];
}

export type DeleteFlag = 'active' | 'deleted';
export type DeleteStatus = 'active' | 'all' | 'deleted';

export function deleteFlagsFromStatus(status: DeleteStatus): DeleteFlag[] {
  return status === 'active' || status === 'deleted' ? [status] : [];
}

/** 未选或同时选择未删除、已删除时表示全部。 */
export function deleteStatusFromFlags(
  values: readonly HeaderFilterValue[],
): DeleteStatus {
  const active = values.includes('active');
  const deleted = values.includes('deleted');
  if (active && !deleted) return 'active';
  if (deleted && !active) return 'deleted';
  return 'all';
}
