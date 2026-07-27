import { ref, shallowRef } from 'vue';

import { createLatestRequestGuard } from './latest-request';

export function createImportValidationSession<Row>() {
  const guard = createLatestRequestGuard();
  const loading = ref(false);
  const rows = shallowRef<Row[]>([]);
  const selectedFile = shallowRef<File | null>(null);

  function reset() {
    guard.invalidate();
    selectedFile.value = null;
    rows.value = [];
    loading.value = false;
  }

  function start(file: File | null) {
    reset();
    if (!file) return null;
    selectedFile.value = file;
    loading.value = true;
    return guard.next();
  }

  function canApply(generation: number) {
    return guard.isLatest(generation);
  }

  function finish(generation: number) {
    if (canApply(generation)) {
      loading.value = false;
    }
  }

  return { canApply, finish, loading, reset, rows, selectedFile, start };
}
