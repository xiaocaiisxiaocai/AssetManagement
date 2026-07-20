import type { UserOptionDto } from '#/api/user';

export function mergeUserOptions(
  current: UserOptionDto[],
  incoming: UserOptionDto[],
) {
  const byId = new Map(current.map((user) => [user.id, user]));
  incoming.forEach((user) => byId.set(user.id, user));
  return [...byId.values()];
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
