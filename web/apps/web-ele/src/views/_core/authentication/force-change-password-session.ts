import type { UserInfo } from '@vben/types';

import { DEFAULT_HOME_PATH, LOGIN_PATH } from '@vben/constants';

type ForceChangePasswordUserInfo = UserInfo & {
  homePath?: string;
  mustChangePassword?: boolean;
};

type AccountDisplayInfo = {
  realName?: string;
  username?: string;
};

export function resolveForceChangePasswordTarget(
  userInfo: ForceChangePasswordUserInfo | null,
) {
  if (!userInfo) {
    return LOGIN_PATH;
  }
  if (userInfo.mustChangePassword) {
    return null;
  }
  return userInfo.homePath || DEFAULT_HOME_PATH;
}

export function formatForceChangePasswordAccount(
  userInfo: AccountDisplayInfo | null,
) {
  const username = userInfo?.username?.trim();
  const realName = userInfo?.realName?.trim();
  if (username && realName) {
    return `${username} / ${realName}`;
  }
  return username || realName || '当前账号';
}
