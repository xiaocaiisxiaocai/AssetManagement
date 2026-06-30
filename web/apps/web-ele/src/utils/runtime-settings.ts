import type { RuntimeSettings } from '#/api/base-data';

import { getRuntimeSettingsApi } from '#/api/base-data';

const FALLBACK_PAGE_SIZE = 20;
const DEFAULT_PAGE_SIZE_OPTIONS = [10, 20, 50, 100];

let runtimeSettingsPromise: null | Promise<RuntimeSettings> = null;

export async function getRuntimeSettings() {
  runtimeSettingsPromise ??= getRuntimeSettingsApi().catch((error) => {
    runtimeSettingsPromise = null;
    throw error;
  });
  return runtimeSettingsPromise;
}

export function invalidateRuntimeSettings() {
  runtimeSettingsPromise = null;
}

export async function getDefaultPageSize() {
  try {
    const settings = await getRuntimeSettings();
    return normalizePageSize(settings.pageSize);
  } catch {
    return FALLBACK_PAGE_SIZE;
  }
}

export function createPageSizeOptions(pageSize: number) {
  return [...new Set([...DEFAULT_PAGE_SIZE_OPTIONS, normalizePageSize(pageSize)])]
    .sort((a, b) => a - b);
}

function normalizePageSize(value: number) {
  return Number.isFinite(value) && value > 0 ? Math.trunc(value) : FALLBACK_PAGE_SIZE;
}
