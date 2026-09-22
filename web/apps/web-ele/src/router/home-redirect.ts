import { DEFAULT_HOME_PATH } from '@vben/constants';

export function authenticatedRootRedirect(
  path: string,
  userHomePath?: null | string,
) {
  if (path !== '/') return null;
  return userHomePath || DEFAULT_HOME_PATH;
}
