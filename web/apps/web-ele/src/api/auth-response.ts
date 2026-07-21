export function isLoginRequestUrl(url?: string) {
  if (!url) return false;

  try {
    const path = new URL(url, 'http://asset-management.local').pathname.replace(
      /\/+$/,
      '',
    );
    return path === '/auth/login' || path === '/api/auth/login';
  } catch {
    return false;
  }
}
