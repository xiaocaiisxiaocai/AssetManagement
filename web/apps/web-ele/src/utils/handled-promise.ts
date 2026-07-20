export function runHandled(promise: Promise<unknown>) {
  void promise.catch(() => undefined);
}
