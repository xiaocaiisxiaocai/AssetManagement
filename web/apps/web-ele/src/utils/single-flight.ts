export function createSingleFlight<Args extends unknown[], Result>(
  task: (...args: Args) => Promise<Result>,
) {
  let active: null | Promise<Result> = null;

  return (...args: Args): Promise<Result> => {
    if (active) return active;
    active = task(...args).finally(() => {
      active = null;
    });
    return active;
  };
}
