import type { UserOptionDto } from '#/api/user';

import type { Ref } from 'vue';

import {
  getDepartmentManagerOptionsApi,
  getUserListApi,
  getUserOptionsPageApi,
} from '#/api/user';
import { createLatestRequestGuard } from '#/utils/latest-request';
import { replaceUserOptions } from '#/utils/user-options';

export function createDepartmentUserSearch(context: {
  editingId: Ref<null | number>;
  form: { managerId?: number };
  hasAccessByCodes: (codes: string[]) => boolean;
  userOptions: Ref<UserOptionDto[]>;
  userOptionsLoading: Ref<boolean>;
}) {
  const userOptionsRequestGuard = createLatestRequestGuard();

  async function searchUsers(keyword = '') {
    const requestGeneration = userOptionsRequestGuard.next();
    context.userOptionsLoading.value = true;
    try {
      const canChooseManager =
        context.hasAccessByCodes(['department:create']) ||
        context.hasAccessByCodes(['department:edit']);
      let incoming: UserOptionDto[];
      if (canChooseManager) {
        incoming = await getDepartmentManagerOptionsApi(
          keyword,
          context.editingId.value ?? undefined,
        );
      } else if (
        context.hasAccessByCodes(['approval:create']) ||
        context.hasAccessByCodes(['material-flow:transfer'])
      ) {
        const response = await getUserOptionsPageApi(keyword, 1, 50);
        incoming = response.items;
      } else {
        const response = await getUserListApi(keyword, 1, 50);
        incoming = response.items.filter((user) => user.isActive);
      }
      if (!userOptionsRequestGuard.isLatest(requestGeneration)) return;
      context.userOptions.value = replaceUserOptions(
        context.userOptions.value,
        incoming,
        [context.form.managerId],
      );
    } catch {
      // 请求层已提示，保留已回填选项。
    } finally {
      if (userOptionsRequestGuard.isLatest(requestGeneration))
        context.userOptionsLoading.value = false;
    }
  }

  return { searchUsers };
}
