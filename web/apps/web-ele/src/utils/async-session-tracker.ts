export function createAsyncSessionTracker() {
  let generation = 0;
  let pending = new Set<Promise<unknown>>();

  function start() {
    generation += 1;
    pending = new Set();
    return generation;
  }

  function close() {
    generation += 1;
    pending = new Set();
    return generation;
  }

  function track<T>(promise: Promise<T>, token = generation) {
    if (token !== generation) return promise;
    const sessionPending = pending;
    sessionPending.add(promise);
    void promise.then(
      () => sessionPending.delete(promise),
      () => sessionPending.delete(promise),
    );
    return promise;
  }

  return {
    close,
    hasPending(token = generation) {
      return token === generation && pending.size > 0;
    },
    isCurrent(token: number) {
      return token === generation;
    },
    pending(token = generation) {
      return token === generation ? [...pending] : [];
    },
    start,
    token() {
      return generation;
    },
    track,
  };
}
