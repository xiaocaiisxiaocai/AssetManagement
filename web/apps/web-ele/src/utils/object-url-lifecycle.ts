export function createObjectUrlLifecycle(revoke = URL.revokeObjectURL) {
  let generation = 0;
  let active = false;
  const owned = new Set<string>();

  function revokeOwned(url: string) {
    if (!url.startsWith('blob:')) return;
    owned.delete(url);
    revoke(url);
  }

  return {
    adopt(url: string, uploadGeneration: number) {
      if (!url.startsWith('blob:')) return true;
      if (!active || uploadGeneration !== generation) {
        revoke(url);
        return false;
      }
      owned.add(url);
      return true;
    },
    close() {
      active = false;
      generation += 1;
      for (const url of owned) revoke(url);
      owned.clear();
    },
    open() {
      active = true;
      generation += 1;
      return generation;
    },
    revoke: revokeOwned,
    token() {
      return generation;
    },
  };
}
