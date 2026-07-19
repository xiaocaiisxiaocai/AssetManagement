export function createLatestRequestGuard() {
  let generation = 0;

  return {
    invalidate() {
      generation += 1;
    },
    isLatest(requestGeneration: number) {
      return requestGeneration === generation;
    },
    next() {
      generation += 1;
      return generation;
    },
  };
}
