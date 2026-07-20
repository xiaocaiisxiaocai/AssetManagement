export async function mapWithConcurrency<Input, Output>(
  items: Input[],
  concurrency: number,
  mapper: (item: Input, index: number) => Promise<Output>,
): Promise<Output[]> {
  const results: Output[] = [];
  results.length = items.length;
  let nextIndex = 0;
  const workerCount = Math.min(Math.max(1, concurrency), items.length);

  await Promise.all(
    Array.from({ length: workerCount }, async () => {
      while (nextIndex < items.length) {
        const index = nextIndex++;
        results[index] = await mapper(items[index] as Input, index);
      }
    }),
  );
  return results;
}
