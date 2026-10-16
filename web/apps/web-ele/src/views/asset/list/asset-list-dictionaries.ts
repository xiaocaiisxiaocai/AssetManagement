import type { AssetListState } from './asset-list-state';

import type {
  CategoryNode,
  DepartmentNode,
  DepartmentOptionNode,
} from '#/api/base-data';
import type { UserOptionDto } from '#/api/user';

import {
  getCategoryTreeApi,
  getDepartmentOptionsApi,
  getDepartmentTreeApi,
} from '#/api/base-data';
import {
  getUserListApi,
  getUserOptionsApi,
  getUserOptionsPageApi,
} from '#/api/user';
import { replaceUserOptions } from '#/utils/user-options';

type FlatOption = {
  code?: string;
  id: number;
  isActive?: boolean;
  label: string;
};

export type AssetDictionaryPage = {
  categories: AssetListState['categories'];
  departments: AssetListState['departments'];
  hasAccessByCodes: (codes: string[]) => boolean;
  query: AssetListState['query'];
  userOptionsLoading: AssetListState['userOptionsLoading'];
  userOptionsRequestGuard: AssetListState['userOptionsRequestGuard'];
  users: AssetListState['users'];
};

export function bindAssetDictionaryQueries(page: AssetDictionaryPage) {
  const {
    categories,
    departments,
    hasAccessByCodes,
    query,
    userOptionsLoading,
    userOptionsRequestGuard,
    users,
  } = page;

  async function searchUsers(
    keyword = '',
    keepIds: Array<null | number | undefined> = [],
  ) {
    const requestGeneration = userOptionsRequestGuard.next();
    userOptionsLoading.value = true;
    try {
      const canUseBusinessOptions =
        hasAccessByCodes(['approval:create']) ||
        hasAccessByCodes(['asset:create']) ||
        hasAccessByCodes(['asset:edit']);
      let incoming: UserOptionDto[] = [];
      if (canUseBusinessOptions) {
        const response = await getUserOptionsPageApi(keyword, 1, 50);
        incoming = response.items;
      } else if (hasAccessByCodes(['user:view'])) {
        const response = await getUserListApi(keyword, 1, 50);
        incoming = response.items.filter((user) => user.isActive);
      }
      if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
      users.value = replaceUserOptions(users.value, incoming, [
        ...query.custodianIds,
        ...keepIds,
      ]);
    } catch {
      // 请求层已提示，保留已回填选项。
    } finally {
      if (userOptionsRequestGuard.isLatest(requestGeneration))
        userOptionsLoading.value = false;
    }
  }

  async function loadDictionaries() {
    let departmentRequest: Promise<DepartmentNode[] | DepartmentOptionNode[]> =
      Promise.resolve([]);
    if (hasAccessByCodes(['department:view'])) {
      departmentRequest = getDepartmentTreeApi();
    } else if (
      hasAccessByCodes(['asset:create']) ||
      hasAccessByCodes(['asset:edit'])
    ) {
      departmentRequest = getDepartmentOptionsApi();
    }

    let userRequest = Promise.resolve<UserOptionDto[]>([]);
    if (
      hasAccessByCodes(['approval:create']) ||
      hasAccessByCodes(['asset:create']) ||
      hasAccessByCodes(['asset:edit'])
    ) {
      userRequest = getUserOptionsApi();
    } else if (hasAccessByCodes(['user:view'])) {
      userRequest = getUserListApi('', 1, 50).then((result) => result.items);
    }

    const requests = await Promise.allSettled([
      hasAccessByCodes(['category:view'])
        ? getCategoryTreeApi()
        : Promise.resolve([]),
      departmentRequest,
      userRequest,
    ]);
    if (requests[0].status === 'fulfilled')
      categories.value = requests[0].value;
    if (requests[1].status === 'fulfilled')
      departments.value = requests[1].value;
    if (requests[2].status === 'fulfilled') users.value = requests[2].value;
  }

  function flattenCategories(nodes: CategoryNode[], level = 0): FlatOption[] {
    return nodes.flatMap((node) => {
      const children = flattenCategories(node.children, level + 1);
      if (level !== 2) return children;
      return [
        {
          code: node.code,
          id: node.id,
          label: node.code,
        },
        ...children,
      ];
    });
  }

  return {
    flattenCategories,
    loadDictionaries,
    searchUsers,
  };
}
