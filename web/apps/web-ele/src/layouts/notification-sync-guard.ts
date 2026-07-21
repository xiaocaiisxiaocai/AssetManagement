import { createLatestRequestGuard } from '#/utils/latest-request';

export function createNotificationSyncGuard() {
  const refreshGuard = createLatestRequestGuard();
  let pendingMutations = 0;
  let mutationQueue = Promise.resolve();

  function finishMutation() {
    pendingMutations -= 1;
    refreshGuard.invalidate();
  }

  return {
    enqueueMutation<Result>(task: () => Promise<Result>) {
      pendingMutations += 1;
      refreshGuard.invalidate();

      const result = mutationQueue.then(task);
      mutationQueue = result.then(finishMutation, finishMutation);
      return result;
    },
    beginRefresh() {
      return pendingMutations > 0 ? null : refreshGuard.next();
    },
    canCommitRefresh(generation: null | number) {
      return (
        generation !== null &&
        pendingMutations === 0 &&
        refreshGuard.isLatest(generation)
      );
    },
    invalidate() {
      refreshGuard.invalidate();
    },
  };
}
