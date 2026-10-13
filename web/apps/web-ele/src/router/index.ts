import {
  createRouter,
  createWebHashHistory,
  createWebHistory,
} from 'vue-router';

import { ELEMENT_ID_MAIN_CONTENT } from '@vben/constants';
import { resetStaticRoutes } from '@vben/utils';

import { createRouterGuard } from './guard';
import {
  MAIN_SCROLL_RESTORE_TIMEOUT_MS,
  resolveMainContentScroll,
  resolveRestoredScrollTop,
  shouldRememberMainScroll,
} from './main-content-scroll';
import { routes } from './routes';

/** 按 vue-router 的 history.state.position 记住主内容区滚动，供前进/后退恢复。 */
const mainScrollByPosition = new Map<number, { left: number; top: number }>();

/** 新的导航会使上一次尚未完成的还原失效。 */
let mainScrollRestoreToken = 0;

function mainContentElement() {
  return document.querySelector<HTMLElement>(`#${ELEMENT_ID_MAIN_CONTENT}`);
}

function rememberMainScroll() {
  const position = window.history.state?.position;
  const main = mainContentElement();
  if (typeof position !== 'number' || !main) {
    return;
  }
  mainScrollByPosition.set(position, {
    left: main.scrollLeft,
    top: main.scrollTop,
  });
}

function scrollMainTo(left: number, top: number) {
  mainContentElement()?.scrollTo({ left, top });
}

/**
 * 不阻塞导航。进场完成前内容高度不足，等可滚动高度够了再还原。
 */
function restoreMainScrollWhenReady(
  saved: { left: number; top: number },
  token: number,
) {
  const startedAt = performance.now();

  const attempt = () => {
    if (token !== mainScrollRestoreToken) {
      return;
    }
    const main = mainContentElement();
    const elapsedMs = performance.now() - startedAt;
    if (!main) {
      if (elapsedMs >= MAIN_SCROLL_RESTORE_TIMEOUT_MS) {
        return;
      }
      requestAnimationFrame(attempt);
      return;
    }
    const decision = resolveRestoredScrollTop({
      clientHeight: main.clientHeight,
      elapsedMs,
      savedTop: saved.top,
      scrollHeight: main.scrollHeight,
    });
    if (!decision.ready) {
      requestAnimationFrame(attempt);
      return;
    }
    main.scrollTo({ left: saved.left, top: decision.top });
  };

  requestAnimationFrame(attempt);
}

/**
 *  @zh_CN 创建vue-router实例
 */
const router = createRouter({
  history:
    import.meta.env.VITE_ROUTER_HISTORY === 'hash'
      ? createWebHashHistory(import.meta.env.VITE_BASE)
      : createWebHistory(import.meta.env.VITE_BASE),
  // 应该添加到路由的初始路由列表。
  routes,
  scrollBehavior: (to, _from, savedPosition) => {
    const token = ++mainScrollRestoreToken;
    const action = resolveMainContentScroll({
      hash: to.hash,
      savedPosition,
    });
    if (action === 'hash') {
      return { behavior: 'smooth', el: to.hash };
    }
    if (action === 'saved-position') {
      const position = window.history.state?.position;
      const saved =
        typeof position === 'number'
          ? mainScrollByPosition.get(position)
          : undefined;
      if (saved) {
        restoreMainScrollWhenReady(saved, token);
      } else {
        scrollMainTo(0, 0);
      }
      return savedPosition ?? { left: 0, top: 0 };
    }
    scrollMainTo(0, 0);
    return { left: 0, top: 0 };
  },
  // 是否应该禁止尾部斜杠。
  // strict: true,
});

// 离开页面前记下主内容区滚动。须早于业务守卫，且不拦截导航。
// 前进/后退时 history 已是目标条目，跳过，避免覆盖该条目原先的滚动。
router.beforeEach((to) => {
  if (!shouldRememberMainScroll(window.history.state?.current, to.fullPath)) {
    return;
  }
  rememberMainScroll();
});

const resetRoutes = () => resetStaticRoutes(router, routes);

// 创建路由守卫
createRouterGuard(router);

export { resetRoutes, router };
