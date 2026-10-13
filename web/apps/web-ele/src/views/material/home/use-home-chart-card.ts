import type { TestProjectStats } from '#/api/test-project';

import { nextTick, onBeforeUnmount, watch } from 'vue';

export function useHomeChartCard(
  getStats: () => null | TestProjectStats,
  render: (data: TestProjectStats) => void,
) {
  let themeObserver: MutationObserver | undefined;
  let disposed = false;

  function ensureThemeObserver() {
    if (themeObserver) return;
    themeObserver = new MutationObserver(() => {
      const data = getStats();
      if (data) render(data);
    });
    themeObserver.observe(document.documentElement, {
      attributeFilter: ['class', 'data-theme', 'style'],
      attributes: true,
    });
  }

  watch(getStats, async (data) => {
    if (!data) return;
    await nextTick();
    if (disposed) return;
    render(data);
    ensureThemeObserver();
  });

  onBeforeUnmount(() => {
    disposed = true;
    themeObserver?.disconnect();
  });
}
