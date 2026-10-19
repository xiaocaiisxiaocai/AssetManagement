export interface WorkspaceRequestHandlers<T> {
  enabled: boolean;
  onError: (error: unknown) => void;
  onSettled: () => void;
  onStart: () => void;
  onSuccess: (value: T) => void;
  projectId: number;
  request: (projectId: number) => Promise<T>;
}

export function createWorkspaceRequestCoordinator() {
  let generation = 0;

  return {
    invalidate() {
      generation += 1;
    },
    async load<T>(handlers: WorkspaceRequestHandlers<T>) {
      const requestGeneration = ++generation;
      if (!handlers.enabled || handlers.projectId <= 0) return;
      handlers.onStart();
      try {
        const value = await handlers.request(handlers.projectId);
        if (requestGeneration === generation) handlers.onSuccess(value);
      } catch (error) {
        if (requestGeneration === generation) handlers.onError(error);
      } finally {
        if (requestGeneration === generation) handlers.onSettled();
      }
    },
  };
}
