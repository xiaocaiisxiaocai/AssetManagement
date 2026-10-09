import type { UserOptionDto } from '#/api/user';

export function mergeUserOptions(
  current: UserOptionDto[],
  incoming: UserOptionDto[],
) {
  const byId = new Map(current.map((user) => [user.id, user]));
  incoming.forEach((user) => byId.set(user.id, user));
  return [...byId.values()];
}

export function replaceUserOptions(
  current: UserOptionDto[],
  incoming: UserOptionDto[],
  keepIds: Array<null | number | undefined> = [],
) {
  const keep = new Set(keepIds.filter((id): id is number => !!id));
  const byId = new Map<number, UserOptionDto>();
  current.forEach((user) => {
    if (keep.has(user.id)) byId.set(user.id, user);
  });
  incoming.forEach((user) => byId.set(user.id, user));
  return [...byId.values()];
}

export function selectableTransferees(
  users: UserOptionDto[],
  excludedIds: Array<null | number | undefined> = [],
) {
  const excluded = new Set(excludedIds.filter((id): id is number => !!id));
  return users.filter((user) => !!user.departmentId && !excluded.has(user.id));
}

export function mergeSelectedUserOption(
  current: UserOptionDto[],
  selected: {
    employeeNo?: null | string;
    id?: null | number;
    name?: null | string;
  },
) {
  if (!selected.id || !selected.name) return current;
  return mergeUserOptions(current, [
    {
      employeeNo: selected.employeeNo ?? '',
      id: selected.id,
      name: selected.name,
    },
  ]);
}
