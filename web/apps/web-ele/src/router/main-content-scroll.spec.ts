import { describe, expect, it } from 'vitest';

import {
  MAIN_SCROLL_RESTORE_TIMEOUT_MS,
  resolveMainContentScroll,
  resolveRestoredScrollTop,
  shouldRememberMainScroll,
} from './main-content-scroll';

describe('主内容区滚动决策', () => {
  it('浏览器前进后退优先于 hash', () => {
    expect(
      resolveMainContentScroll({
        hash: '#section',
        savedPosition: { left: 0, top: 120 },
      }),
    ).toBe('saved-position');
  });

  it('只有 hash 时保持锚点滚动', () => {
    expect(
      resolveMainContentScroll({
        hash: '#section',
        savedPosition: null,
      }),
    ).toBe('hash');
  });

  it('普通路由切换把主内容区滚回顶部', () => {
    expect(
      resolveMainContentScroll({
        hash: '',
        savedPosition: null,
      }),
    ).toBe('reset');
  });

  it('只有离开当前历史条目时才记录滚动', () => {
    expect(shouldRememberMainScroll('/asset/list', '/approval/mine')).toBe(
      true,
    );
    expect(shouldRememberMainScroll(undefined, '/asset/list')).toBe(true);
    expect(shouldRememberMainScroll('/asset/list', '/asset/list')).toBe(false);
  });
});

describe('前进后退滚动还原', () => {
  it('可滚动高度足够时滚到保存位置', () => {
    expect(
      resolveRestoredScrollTop({
        clientHeight: 800,
        elapsedMs: 0,
        savedTop: 305,
        scrollHeight: 1200,
      }),
    ).toEqual({ ready: true, top: 305 });
  });

  it('进场前高度不够且未超时，继续等待', () => {
    expect(
      resolveRestoredScrollTop({
        clientHeight: 242,
        elapsedMs: MAIN_SCROLL_RESTORE_TIMEOUT_MS - 1,
        savedTop: 305,
        scrollHeight: 400,
      }),
    ).toEqual({ ready: false, top: 158 });
  });

  it('超时后滚到当前能到达的最大值', () => {
    expect(
      resolveRestoredScrollTop({
        clientHeight: 242,
        elapsedMs: MAIN_SCROLL_RESTORE_TIMEOUT_MS,
        savedTop: 305,
        scrollHeight: 400,
      }),
    ).toEqual({ ready: true, top: 158 });
  });

  it('可滚动高度刚好等于保存位置时立即还原', () => {
    expect(
      resolveRestoredScrollTop({
        clientHeight: 100,
        elapsedMs: 0,
        savedTop: 305,
        scrollHeight: 405,
      }),
    ).toEqual({ ready: true, top: 305 });
  });
});
