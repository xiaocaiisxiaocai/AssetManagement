export type MainContentScrollAction = 'hash' | 'reset' | 'saved-position';

/** 前进/后退还原滚动时，等待进场内容变高的最长时间。 */
export const MAIN_SCROLL_RESTORE_TIMEOUT_MS = 1000;

/**
 * 离开页的 Transition 尚未结束时，主内容区高度不够，直接 scrollTo 会被截断。
 * 可滚动高度足够则滚到保存位置；超时则滚到当前能到达的最大值。
 */
export function resolveRestoredScrollTop(input: {
  clientHeight: number;
  elapsedMs: number;
  savedTop: number;
  scrollHeight: number;
}): { ready: boolean; top: number } {
  const maxTop = Math.max(0, input.scrollHeight - input.clientHeight);
  const savedTop = Math.max(0, input.savedTop);
  if (maxTop >= savedTop) {
    return { ready: true, top: savedTop };
  }
  if (input.elapsedMs >= MAIN_SCROLL_RESTORE_TIMEOUT_MS) {
    return { ready: true, top: maxTop };
  }
  return { ready: false, top: maxTop };
}

/**
 * 浏览器前进/后退时，history.state 已切到目标条目。
 * 此时不能再按该 position 记滚动，否则会覆盖离开目标页时保存的位置。
 */
export function shouldRememberMainScroll(
  historyCurrent: unknown,
  toFullPath: string,
) {
  return historyCurrent !== toFullPath;
}

/**
 * 决定路由切换后如何处理主内容区滚动。
 * 浏览器前进/后退的 savedPosition 优先于 hash，与原 scrollBehavior 一致。
 */
export function resolveMainContentScroll(input: {
  hash: string;
  savedPosition: unknown;
}): MainContentScrollAction {
  if (input.savedPosition) {
    return 'saved-position';
  }
  if (input.hash) {
    return 'hash';
  }
  return 'reset';
}
