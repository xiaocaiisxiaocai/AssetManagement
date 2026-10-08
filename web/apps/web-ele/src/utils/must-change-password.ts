import { LOGIN_PATH } from '@vben/constants';

/** 读取登录令牌里的必须改密标记。签名由后端校验，这里只决定是否留下改密入口。 */
export function tokenRequiresPasswordChange(
  token: null | string | undefined,
) {
  const payload = readJwtPayload(token);
  const value = payload?.mustChangePassword;
  return value === true || value === 'true';
}

/** 仍须改密时只允许停在登录页，其余地址都回到登录页。 */
export function passwordChangeRedirect(
  path: string,
  token: null | string | undefined,
) {
  if (!tokenRequiresPasswordChange(token) || path === LOGIN_PATH) return null;
  return LOGIN_PATH;
}

function readJwtPayload(token: null | string | undefined) {
  const segment = token?.split('.')[1];
  if (!segment) return null;
  try {
    const padded = segment
      .replace(/-/g, '+')
      .replace(/_/g, '/')
      .padEnd(Math.ceil(segment.length / 4) * 4, '=');
    return JSON.parse(decodeBase64Url(padded)) as {
      mustChangePassword?: boolean | string;
    };
  } catch {
    return null;
  }
}

function decodeBase64Url(value: string) {
  const binary = atob(value);
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}
